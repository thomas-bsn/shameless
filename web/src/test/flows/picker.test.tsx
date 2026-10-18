import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { json, mockApi, problem } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Picker = Schemas["PickerResponse"];
type Participant = Schemas["GameParticipantDto"];

const participant = (playerId: number, cost: number, overrides: Partial<Participant> = {}): Participant => ({
  playerId,
  prenom: `Joueur${playerId}`,
  riotId: `J${playerId}#EUW`,
  rankTier: null,
  rankDivision: null,
  cost,
  role: null,
  ...overrides,
});

const dix = Array.from({ length: 10 }, (_, i) => participant(i + 1, 3));

const picker = (overrides: Partial<Picker> = {}): Picker => ({
  players: dix,
  methode: null,
  variante: null,
  teamA: [],
  teamB: [],
  teamACost: 0,
  teamBCost: 0,
  ecartCost: 0,
  canValidate: false,
  capitaineA: null,
  capitaineB: null,
  premierPick: null,
  picks: [],
  equipeAuTrait: null,
  ...overrides,
});

const tirage = (): Picker =>
  picker({
    methode: "Roulette",
    variante: "Equilibree",
    teamA: dix.slice(0, 5).map((p, i) => ({ ...p, cost: 5 - i * 0.5 })),
    teamB: dix.slice(5).map((p, i) => ({ ...p, cost: 4.8 - i * 0.5 })),
    teamACost: 17.3,
    teamBCost: 17.7,
    ecartCost: 0.4,
    canValidate: true,
  });

const soireeAvec = (p: Picker | null): Schemas["OpenSoireeResponse"] => ({
  soiree: { id: 7, status: "Open", openedAt: "2026-10-16T19:00:00Z", closedAt: null, picker: p },
});

const joueurs = Array.from({ length: 10 }, (_, i) =>
  player({ id: i + 1, prenom: `Joueur${i + 1}`, riotId: `J${i + 1}#EUW` }),
);

const etapes = () => within(screen.getByRole("list", { name: "Étapes du Picker" }));

// jsdom n'a pas ResizeObserver, dont les cases à cocher de Radix ont besoin dans un formulaire.
beforeEach(() => {
  vi.stubGlobal(
    "ResizeObserver",
    class {
      observe() {}
      unobserve() {}
      disconnect() {}
    },
  );
});

function stubMotion(reduce: boolean) {
  vi.stubGlobal("matchMedia", (query: string) => ({
    matches: reduce && query.includes("reduce"),
    addEventListener() {},
    removeEventListener() {},
  }));
}

describe("Picker : parcours en six étapes", () => {
  it("montre les six étapes et met en avant la courante", async () => {
    mockApi({ "GET /auth/me": user(), "GET /soirees/open": soireeAvec(null), "GET /players": joueurs });
    renderApp("/soiree/picker");

    await screen.findByRole("checkbox", { name: /Joueur1\b/ });
    const noms = etapes()
      .getAllByRole("listitem")
      .map((li) => li.textContent);
    expect(noms).toEqual(["1Joueurs", "2Méthode", "3Draft", "4Équipes", "5Autorole", "6Série"]);
    expect(etapes().getByText("Joueurs").closest("li")).toHaveAttribute("aria-current", "step");
  });

  it("compte les joueurs et indique combien il en reste avant la méthode", async () => {
    mockApi({ "GET /auth/me": user(), "GET /soirees/open": soireeAvec(null), "GET /players": joueurs });
    renderApp("/soiree/picker");

    await userEvent.click(await screen.findByRole("checkbox", { name: /Joueur1\b/ }));
    await userEvent.click(screen.getByRole("checkbox", { name: /Joueur2\b/ }));

    expect(screen.getByText("2/10")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Choisir la méthode · encore 8 joueurs" })).toBeDisabled();
  });

  it("affiche un bandeau avec Réessayer quand les joueurs ne se chargent pas", async () => {
    let appels = 0;
    mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": soireeAvec(null),
      "GET /players": () => (++appels === 1 ? problem(500, "Les joueurs sont injoignables.") : json(joueurs)),
    });
    renderApp("/soiree/picker");

    expect(await screen.findByText("Les joueurs sont injoignables.")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Réessayer" }));

    expect(await screen.findByRole("checkbox", { name: /Joueur1\b/ })).toBeInTheDocument();
  });
});

describe("Picker : Roulette", () => {
  function mockRoulette() {
    let courant: Picker | null = picker({ methode: null });
    return mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": () => json(soireeAvec(courant)),
      "GET /players": joueurs,
      "POST /soirees/open/picker/roulette": () => {
        courant = tirage();
        return json(courant);
      },
    });
  }

  it("déroule le tirage dans une fenêtre puis affiche le résultat avec l'écart", async () => {
    stubMotion(false);
    mockRoulette();
    renderApp("/soiree/picker");

    await userEvent.click(await screen.findByRole("button", { name: "Roulette Équilibrée" }));
    const fenetre = await screen.findByRole("dialog", { name: "Tirage en cours" });
    await userEvent.click(within(fenetre).getByRole("button", { name: "Passer l'animation" }));

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(await screen.findByText("Voilà les équipes")).toBeInTheDocument();
    expect(screen.getByText("Écart de cost : 0.4 pts")).toBeInTheDocument();
    expect(within(screen.getByRole("group", { name: "Équipe A" })).getByText("17.3 pts")).toBeInTheDocument();
    expect(within(screen.getByRole("group", { name: "Équipe B" })).getByText("17.7 pts")).toBeInTheDocument();
    expect(etapes().getByText("Équipes").closest("li")).toHaveAttribute("aria-current", "step");
  });

  it("saute le mouvement du tirage quand l'utilisateur réduit les animations", async () => {
    stubMotion(true);
    mockRoulette();
    renderApp("/soiree/picker");

    await userEvent.click(await screen.findByRole("button", { name: "Roulette Équilibrée" }));

    expect(await screen.findByText("Voilà les équipes")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("permet de relancer le tirage", async () => {
    stubMotion(true);
    const fetchMock = mockRoulette();
    renderApp("/soiree/picker");
    await userEvent.click(await screen.findByRole("button", { name: "Roulette Équilibrée" }));

    await userEvent.click(await screen.findByRole("button", { name: "Relancer le tirage" }));

    await vi.waitFor(() =>
      expect(
        fetchMock.mock.calls.filter(([r]) => r.method === "POST" && r.url.endsWith("/picker/roulette")),
      ).toHaveLength(2),
    );
  });
});

describe("Picker : Autorole puis Série", () => {
  function mockEquipes() {
    let creation: unknown = null;
    const fetchMock = mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": soireeAvec(tirage()),
      "GET /players": joueurs,
      "POST /soirees/open/picker/validate": async (req) => {
        creation = await req.json();
        return json({ seriesId: 5 });
      },
      "POST /soirees/open/picker/autorole": json(tirage()),
    });
    return { fetchMock, creation: () => creation };
  }

  it("passe l'autorole pour aller à la création de la série", async () => {
    stubMotion(true);
    mockEquipes();
    renderApp("/soiree/picker");

    await userEvent.click(await screen.findByRole("button", { name: "Garder ces équipes" }));
    expect(etapes().getByText("Autorole").closest("li")).toHaveAttribute("aria-current", "step");
    await userEvent.click(screen.getByRole("button", { name: "Passer" }));

    expect(etapes().getByText("Série").closest("li")).toHaveAttribute("aria-current", "step");
    expect(screen.getByRole("button", { name: "Créer la série" })).toBeInTheDocument();
  });

  it("lance l'autorole puis passe à la série", async () => {
    stubMotion(true);
    const { fetchMock } = mockEquipes();
    renderApp("/soiree/picker");

    await userEvent.click(await screen.findByRole("button", { name: "Garder ces équipes" }));
    await userEvent.click(screen.getByRole("button", { name: "Lancer l'Autorole" }));

    await vi.waitFor(() => expect(fetchMock.mock.calls.some(([r]) => r.url.endsWith("/picker/autorole"))).toBe(true));
    expect(await screen.findByRole("button", { name: "Créer la série" })).toBeInTheDocument();
  });

  it("crée la série avec le format et Fearless choisis", async () => {
    stubMotion(true);
    const { creation } = mockEquipes();
    renderApp("/soiree/picker");
    await userEvent.click(await screen.findByRole("button", { name: "Garder ces équipes" }));
    await userEvent.click(screen.getByRole("button", { name: "Passer" }));

    await userEvent.click(screen.getByRole("radio", { name: "BO5" }));
    await userEvent.click(screen.getByRole("checkbox", { name: "Fearless" }));
    await userEvent.click(screen.getByRole("button", { name: "Créer la série" }));

    await vi.waitFor(() => expect(creation()).toMatchObject({ format: 5, fearless: true }));
  });
});
