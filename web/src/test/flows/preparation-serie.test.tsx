import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { json, mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Participant = Schemas["GameParticipantDto"];
type Series = Schemas["SeriesResponse"];

const participant = (playerId: number, prenom: string): Participant => ({
  playerId,
  prenom,
  riotId: `${prenom}#EUW`,
  rankTier: null,
  rankDivision: null,
  cost: 3,
  role: null,
});

const equipeA = [participant(1, "Aldric"), participant(2, "Kuro")];
const equipeB = [participant(3, "Mina"), participant(4, "Elio")];

const serie = (overrides: Partial<Series> = {}): Series => ({
  seriesId: 5,
  soireeId: 7,
  format: 3,
  fearless: true,
  alternateSides: true,
  outcome: "EnCours",
  winningTeam: null,
  teamAWins: 0,
  teamBWins: 0,
  teamA: equipeA,
  teamB: equipeB,
  capitaineA: null,
  capitaineB: null,
  games: [],
  ...overrides,
});

const liens: Schemas["LiensSerieResponse"] = {
  seriesId: 5,
  lienCapitaineA: "secret-a",
  lienCapitaineB: "secret-b",
  lienSpectateur: "secret-spectateur",
};

const routes = (series: Series = serie()) => ({
  "GET /auth/me": user(),
  "GET /series/5": series,
  "GET /series/5/liens": liens,
  "GET /series/5/code-companion": { seriesId: 5, code: "code-companion-1" },
});

// jsdom n'implémente pas ces API, que le menu déroulant de Radix appelle.
beforeEach(() => {
  Element.prototype.hasPointerCapture ??= () => false;
  Element.prototype.setPointerCapture ??= () => {};
  Element.prototype.releasePointerCapture ??= () => {};
  Element.prototype.scrollIntoView ??= () => {};
});

describe("Écran de préparation", () => {
  it("s'ouvre après « Créer la série » du Picker", async () => {
    mockApi({
      ...routes(),
      "GET /soirees/open": { soiree: { id: 7, status: "Open", openedAt: "2026-10-16T19:04:00Z", closedAt: null, picker: null } },
    });
    const { router } = renderApp("/series/5/preparation");

    expect(await screen.findByRole("heading", { name: "Prépare la série" })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/series/5/preparation");
  });

  it("montre les deux Équipes et les capitaines déjà désignés par la Draft", async () => {
    mockApi(routes(serie({ capitaineA: 2, capitaineB: 3 })));
    renderApp("/series/5/preparation");

    const a = await screen.findByRole("group", { name: "Équipe A" });
    expect(within(a).getByText("Aldric")).toBeInTheDocument();
    expect(within(screen.getByRole("group", { name: "Équipe B" })).getByText("Elio")).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Capitaine de l'Équipe A" })).toHaveTextContent("Kuro");
    expect(screen.getByRole("combobox", { name: "Capitaine de l'Équipe B" })).toHaveTextContent("Mina");
  });

  it("désigne ou change le Capitaine d'une Équipe parmi ses joueurs", async () => {
    let corps: unknown = null;
    const fetchMock = mockApi({
      ...routes(),
      "PUT /series/5/capitaines/A": async (req) => {
        corps = await req.json();
        return json(serie({ capitaineA: 2 }));
      },
    });
    renderApp("/series/5/preparation");

    await userEvent.click(await screen.findByRole("combobox", { name: "Capitaine de l'Équipe A" }));
    expect(screen.getAllByRole("option").map((o) => o.textContent)).toEqual(["Aldric", "Kuro"]);
    await userEvent.click(screen.getByRole("option", { name: "Kuro" }));

    await vi.waitFor(() => expect(corps).toEqual({ designation: "Manuel", playerId: 2 }));
    expect(fetchMock.mock.calls.some(([r]) => r.method === "PUT" && r.url.endsWith("/capitaines/B"))).toBe(false);
  });

  it("montre les deux Liens capitaine, les copie et les renouvelle", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });
    vi.spyOn(window, "confirm").mockReturnValue(true);
    mockApi({
      ...routes(),
      "POST /series/5/liens/A/renouveler": json({ ...liens, lienCapitaineA: "nouveau-a" }),
    });
    renderApp("/series/5/preparation");

    const champ = await screen.findByLabelText("Lien capitaine de l'Équipe A");
    expect(champ).toHaveValue(`${window.location.origin}/draft/capitaine/secret-a`);
    expect(screen.getByLabelText("Lien capitaine de l'Équipe B")).toHaveValue(
      `${window.location.origin}/draft/capitaine/secret-b`,
    );

    const ligneA = champ.closest("div")!.parentElement!;
    await userEvent.click(within(ligneA).getByRole("button", { name: "Copier" }));
    expect(writeText).toHaveBeenCalledWith(`${window.location.origin}/draft/capitaine/secret-a`);

    await userEvent.click(within(ligneA).getByRole("button", { name: "Renouveler" }));
    expect(await screen.findByDisplayValue(`${window.location.origin}/draft/capitaine/nouveau-a`)).toBeInTheDocument();
  });

  it("donne le Lien spectateur dès la création, sans « J'ai envoyé les Liens capitaine »", async () => {
    mockApi(routes());
    renderApp("/series/5/preparation");

    expect(await screen.findByLabelText("Lien spectateur")).toHaveValue(
      `${window.location.origin}/draft/spectateur/secret-spectateur`,
    );
    expect(screen.queryByRole("button", { name: /J'ai envoyé/ })).not.toBeInTheDocument();
  });

  it("montre le Code Companion, le copie et le renouvelle", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    mockApi({
      ...routes(),
      "POST /series/5/code-companion/renouveler": json({ seriesId: 5, code: "code-companion-2" }),
    });
    renderApp("/series/5/preparation");

    expect(await screen.findByLabelText("Code Companion")).toHaveValue("code-companion-1");
    const ligne = screen.getByLabelText("Code Companion").closest("div")!.parentElement!;
    await userEvent.click(within(ligne).getByRole("button", { name: "Renouveler" }));

    expect(await screen.findByDisplayValue("code-companion-2")).toBeInTheDocument();
  });

  it("n'a pas encore de bouton « Lancer la draft »", async () => {
    mockApi(routes());
    renderApp("/series/5/preparation");

    await screen.findByRole("heading", { name: "Prépare la série" });
    expect(screen.queryByRole("button", { name: "Lancer la draft" })).not.toBeInTheDocument();
  });

  it("est réservé à l'organisateur connecté", async () => {
    mockApi({ "GET /auth/me": json({ status: 401 }, 401) });
    const { router } = renderApp("/series/5/preparation");

    await vi.waitFor(() => expect(router.state.location.pathname).toBe("/connexion"));
  });
});

describe("Série : accès aux liens", () => {
  it("renvoie vers l'Écran de préparation par « Liens et code »", async () => {
    mockApi(routes());
    renderApp("/series/5");

    const lien = await screen.findByRole("link", { name: "Liens et code" });
    expect(lien).toHaveAttribute("href", "/series/5/preparation");
    expect(screen.queryByText("J'ai envoyé les Liens capitaine")).not.toBeInTheDocument();
  });
});
