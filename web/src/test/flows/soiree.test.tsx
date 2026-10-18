import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { json, mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Participant = Schemas["GameParticipantDto"];

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

const serie = (overrides: Partial<Schemas["SeriesResponse"]> = {}): Schemas["SeriesResponse"] => ({
  seriesId: 11,
  soireeId: 7,
  format: 3,
  fearless: true,
  alternateSides: true,
  outcome: "EnCours",
  winningTeam: null,
  teamAWins: 1,
  teamBWins: 0,
  teamA: equipeA,
  teamB: equipeB,
  capitaineA: null,
  capitaineB: null,
  games: [
    {
      id: 21,
      status: "completed",
      teamASide: "blue",
      teamBSide: "red",
      winner: "A",
      teamA: equipeA,
      teamB: equipeB,
      riotMatchId: null,
      championsJoues: [],
      rapportEnRevue: null,
    },
    {
      id: 22,
      status: "pending",
      teamASide: "red",
      teamBSide: "blue",
      winner: null,
      teamA: equipeA,
      teamB: equipeB,
      riotMatchId: null,
      championsJoues: [],
      rapportEnRevue: null,
    },
  ],
  ...overrides,
});

const soireeOuverte = (picker: Schemas["PickerResponse"] | null = null): Schemas["OpenSoireeResponse"] => ({
  soiree: { id: 7, status: "Open", openedAt: "2026-10-16T19:04:00Z", closedAt: null, picker },
});

const pickerEnDraft: Schemas["PickerResponse"] = {
  players: Array.from({ length: 10 }, (_, i) => participant(i + 1, `J${i + 1}`)),
  methode: "Draft",
  variante: null,
  teamA: equipeA,
  teamB: [participant(3, "Mina")],
  teamACost: 6,
  teamBCost: 3,
  ecartCost: 3,
  canValidate: false,
  capitaineA: 1,
  capitaineB: 3,
  premierPick: "A",
  picks: [],
  equipeAuTrait: "B",
};

describe("Soirée", () => {
  it("offers a big button to open a Soirée when none is open", async () => {
    const fetch = mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": { soiree: null },
      "POST /soirees": json(soireeOuverte().soiree),
    });
    renderApp("/soiree");

    await userEvent.click(await screen.findByRole("button", { name: "Ouvrir une soirée" }));

    expect(fetch.mock.calls.some(([r]) => r.method === "POST" && r.url.endsWith("/api/soirees"))).toBe(true);
  });

  it("shows the current Série face to face with its score and a link to its game", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": soireeOuverte(),
      "GET /soirees/7/series": { series: [serie()] },
    });
    renderApp("/soiree");

    const face = await screen.findByRole("region", { name: "Série en cours" });
    expect(within(face).getByText("Série 1 · en cours")).toBeInTheDocument();
    expect(within(face).getByText("BO3")).toBeInTheDocument();
    expect(within(face).getByText("Fearless")).toBeInTheDocument();
    expect(within(face).getByText("Aldric")).toBeInTheDocument();
    expect(within(face).getByText("Elio")).toBeInTheDocument();
    expect(within(face).getByLabelText("Score de la série")).toHaveTextContent("1 – 0");
    expect(within(face).getByRole("link", { name: "Voir la game 2" })).toHaveAttribute("href", "/games/22");
  });

  it("resumes the Picker and says how far it got", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": soireeOuverte(pickerEnDraft),
      "GET /soirees/7/series": { series: [] },
    });
    renderApp("/soiree");

    expect(await screen.findByText("Picker en cours · Draft, 3 joueurs sur 10 choisis")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Reprendre le Picker" })).toHaveAttribute("href", "/soiree/picker");
  });

  it("lists the unfolding of the Séries", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": soireeOuverte(),
      "GET /soirees/7/series": {
        series: [serie({ seriesId: 10, format: 1, outcome: "Gagnee", winningTeam: "A", games: [] }), serie()],
      },
    });
    renderApp("/soiree");

    const deroule = await screen.findByRole("list", { name: "Déroulé de la soirée" });
    expect(within(deroule).getAllByRole("listitem").map((li) => li.textContent)).toEqual([
      expect.stringContaining("Série 1 · BO1"),
      expect.stringContaining("Série 2 · BO3"),
    ]);
  });

  it("closes the Soirée only after confirmation", async () => {
    const fetch = mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": soireeOuverte(),
      "GET /soirees/7/series": { series: [] },
      "POST /soirees/7/close": json({}),
    });
    const confirm = vi.fn();
    vi.stubGlobal("confirm", confirm);
    renderApp("/soiree");
    const fermer = () => fetch.mock.calls.filter(([r]) => r.url.endsWith("/soirees/7/close"));

    await userEvent.click(await screen.findByRole("button", { name: "Fermer la soirée" }));
    const fenetre = await screen.findByRole("dialog", { name: "Fermer la soirée ?" });
    expect(within(fenetre).getByText(/séries en cours resteront telles quelles/)).toBeInTheDocument();
    await userEvent.click(within(fenetre).getByRole("button", { name: "Annuler" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(fermer()).toHaveLength(0);

    await userEvent.click(screen.getByRole("button", { name: "Fermer la soirée" }));
    await userEvent.click(within(await screen.findByRole("dialog")).getByRole("button", { name: "Fermer la soirée" }));
    await vi.waitFor(() => expect(fermer()).toHaveLength(1));
    expect(confirm).not.toHaveBeenCalled();
  });

  it("shows a dedicated screen when the Soirée closed by itself during a Picker", async () => {
    const fetch = mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": { soiree: null },
      "POST /soirees": json(soireeOuverte().soiree),
    });
    renderApp("/soiree/picker");

    expect(await screen.findByRole("heading", { name: "La soirée s'est fermée" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Ouvrir une nouvelle soirée" }));

    expect(fetch.mock.calls.some(([r]) => r.method === "POST" && r.url.endsWith("/api/soirees"))).toBe(true);
  });
});
