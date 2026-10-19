import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { json, mockApi } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const admin = () => user({ isAdmin: true });

const pendingGame = (overrides: Partial<Schemas["PendingGameResponse"]> = {}): Schemas["PendingGameResponse"] => ({
  id: 12,
  playedAt: "2026-10-16T19:28:00Z",
  seriesId: 5,
  status: "pending",
  participants: [],
  ...overrides,
});

const series = (overrides: Partial<Schemas["SeriesResponse"]> = {}): Schemas["SeriesResponse"] => ({
  seriesId: 5,
  soireeId: 1,
  format: 3,
  fearless: false,
  outcome: "EnCours",
  winningTeam: null,
  teamAWins: 1,
  teamBWins: 0,
  teamA: [],
  teamB: [],
  capitaineA: null,
  capitaineB: null,
  games: [],
  ...overrides,
});

const cost = (overrides: Partial<Schemas["PlayerCostResponse"]> = {}): Schemas["PlayerCostResponse"] => ({
  playerId: 1,
  name: "Alice",
  mois: "2026-10-01",
  cost: 3,
  tier: "C",
  costDeDepart: 3,
  sourceDuCostDeDepart: "RangSoloQ",
  gamesTerminees: 0,
  victoires: 0,
  winrate: null,
  gamesNotees: 0,
  noteMoyenne: null,
  evolue: false,
  gamesMinimum: 5,
  ajustementPerformance: 0,
  ajustementResultats: 0,
  ...overrides,
});

const calme = {
  "GET /games/pending": [],
  "GET /series/active": { series: [] },
  "GET /players": [],
};

describe("Administration", () => {
  it("refuse clairement un utilisateur qui n'est pas Administrateur, sans appeler les actions", async () => {
    const api = mockApi({ "GET /auth/me": user({ isAdmin: false }), ...calme });
    renderApp("/administration");

    expect(await screen.findByText(/réservée aux administrateurs/i)).toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "Joueurs" })).not.toBeInTheDocument();
    expect(api.mock.calls.some(([req]) => (req as Request).url.includes("/games/pending"))).toBe(false);
  });

  it("dit que tout est en ordre quand il n'y a rien à traiter", async () => {
    mockApi({ "GET /auth/me": admin(), ...calme });
    renderApp("/administration");

    expect(await screen.findByText(/rien à traiter/i)).toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "À traiter" })).not.toBeInTheDocument();
  });

  it("liste les games sans résultat et saisit le gagnant, puis le bandeau disparaît", async () => {
    let pending = [pendingGame()];
    const api = mockApi({
      "GET /auth/me": admin(),
      ...calme,
      "GET /games/pending": () => json(pending),
      "PUT /games/12/result": async (request) => {
        expect(await request.json()).toEqual({ winner: "B" });
        pending = [];
        return json({ gameId: 12, winner: "B", teamASide: "blue" });
      },
    });
    renderApp("/administration");

    const bandeau = await screen.findByRole("region", { name: "À traiter" });
    expect(within(bandeau).getByText(/Game 12/)).toBeInTheDocument();
    await userEvent.click(within(bandeau).getByRole("button", { name: "Victoire Équipe B" }));

    expect(await screen.findByText(/rien à traiter/i)).toBeInTheDocument();
    expect(api.mock.calls.some(([req]) => (req as Request).method === "PUT")).toBe(true);
  });

  it("interrompt une série après une confirmation qui rappelle que les games jouées restent", async () => {
    let actives = [series()];
    const api = mockApi({
      "GET /auth/me": admin(),
      ...calme,
      "GET /series/active": () => json({ series: actives }),
      "POST /series/5/stop": () => {
        actives = [];
        return json({ arret: "Interrompue" });
      },
    });
    renderApp("/administration");

    const bandeau = await screen.findByRole("region", { name: "À traiter" });
    await userEvent.click(within(bandeau).getByRole("button", { name: "Interrompre la série" }));

    const confirmation = await screen.findByRole("alertdialog");
    expect(within(confirmation).getByText(/games jouées restent/i)).toBeInTheDocument();
    expect(api.mock.calls.some(([req]) => (req as Request).url.endsWith("/series/5/stop"))).toBe(false);
    await userEvent.click(within(confirmation).getByRole("button", { name: "Interrompre" }));

    expect(await screen.findByText(/rien à traiter/i)).toBeInTheDocument();
  });

  it("ne fait rien quand la confirmation d'interruption est annulée", async () => {
    const api = mockApi({
      "GET /auth/me": admin(),
      ...calme,
      "GET /series/active": { series: [series()] },
      "POST /series/5/stop": json({ arret: "Interrompue" }),
    });
    renderApp("/administration");

    const bandeau = await screen.findByRole("region", { name: "À traiter" });
    await userEvent.click(within(bandeau).getByRole("button", { name: "Interrompre la série" }));
    await userEvent.click(within(await screen.findByRole("alertdialog")).getByRole("button", { name: "Annuler" }));

    expect(api.mock.calls.some(([req]) => (req as Request).url.endsWith("/series/5/stop"))).toBe(false);
  });

  describe("onglet Joueurs", () => {
    it("règle le cost avec − et +, l'enregistre par ligne et montre la source", async () => {
      let courant = cost({ costDeDepart: 5.6, sourceDuCostDeDepart: "RangSoloQ" });
      const api = mockApi({
        "GET /auth/me": admin(),
        ...calme,
        "GET /players": [player({ id: 1, prenom: "Alice" })],
        "GET /players/1/cost": () => json(courant),
        "PUT /players/1/cost": async (request) => {
          expect(await request.json()).toEqual({ cost: 5.7 });
          courant = cost({ costDeDepart: 5.7, sourceDuCostDeDepart: "FixeParAdmin" });
          return new Response(null, { status: 200 });
        },
      });
      renderApp("/administration");

      const ligne = (await screen.findByText("Alice")).closest("tr") as HTMLElement;
      expect(await within(ligne).findByText("calculé")).toBeInTheDocument();
      expect(within(ligne).getByLabelText("Cost de départ")).toHaveValue("5,6");
      expect(within(ligne).queryByRole("button", { name: "Enregistrer" })).not.toBeInTheDocument();

      await userEvent.click(within(ligne).getByRole("button", { name: "Augmenter le cost" }));
      expect(within(ligne).getByLabelText("Cost de départ")).toHaveValue("5,7");
      expect(within(ligne).getByText(/5,6 avant/)).toBeInTheDocument();
      await userEvent.click(within(ligne).getByRole("button", { name: "Enregistrer" }));

      expect(await within(ligne).findByText("fixé par un admin")).toBeInTheDocument();
      expect(api.mock.calls.some(([req]) => (req as Request).method === "PUT")).toBe(true);
    });

    it("borne le cost entre 1 et 6", async () => {
      mockApi({
        "GET /auth/me": admin(),
        ...calme,
        "GET /players": [player({ id: 1, prenom: "Alice" })],
        "GET /players/1/cost": cost({ costDeDepart: 6 }),
      });
      renderApp("/administration");

      const ligne = (await screen.findByText("Alice")).closest("tr") as HTMLElement;
      await within(ligne).findByText("calculé");
      expect(within(ligne).getByRole("button", { name: "Augmenter le cost" })).toBeDisabled();
    });

    it("renseigne le peak d'un joueur qui n'en a pas", async () => {
      const api = mockApi({
        "GET /auth/me": admin(),
        ...calme,
        "GET /players": [player({ id: 1, prenom: "Alice", peakTier: null, peakLp: 0 })],
        "GET /players/1/cost": cost(),
        "GET /players/peak/saisons": { saisons: ["S24", "S25", "S26"], courante: "S26" },
        "POST /players/1/peak": async (request) => {
          expect(await request.json()).toMatchObject({ peakTier: "DIAMOND", peakDivision: 2, peakSeason: "S25", peakLp: 45 });
          return new Response(null, { status: 200 });
        },
      });
      renderApp("/administration");

      const ligne = (await screen.findByText("Alice")).closest("tr") as HTMLElement;
      await userEvent.click(within(ligne).getByRole("button", { name: "Renseigner le peak" }));
      await userEvent.selectOptions(await within(ligne).findByLabelText("Tier du peak"), "DIAMOND");
      await userEvent.selectOptions(within(ligne).getByLabelText("Division du peak"), "2");
      await userEvent.selectOptions(await within(ligne).findByLabelText("Saison du peak"), "S25");
      await userEvent.clear(within(ligne).getByLabelText("LP du peak"));
      await userEvent.type(within(ligne).getByLabelText("LP du peak"), "45");
      await userEvent.click(within(ligne).getByRole("button", { name: "Enregistrer le peak" }));

      await waitFor(() => expect(api.mock.calls.some(([req]) => (req as Request).method === "POST")).toBe(true));
    });

    it("efface le peak existant", async () => {
      const api = mockApi({
        "GET /auth/me": admin(),
        ...calme,
        "GET /players": [player({ id: 1, prenom: "Alice", peakTier: "DIAMOND", peakDivision: 1, peakLp: 45, peakSeason: "S25" })],
        "GET /players/1/cost": cost(),
        "DELETE /players/1/peak": new Response(null, { status: 200 }),
      });
      renderApp("/administration");

      const ligne = (await screen.findByText("Alice")).closest("tr") as HTMLElement;
      expect(within(ligne).getByText(/Diamond I · 45 LP/)).toBeInTheDocument();
      expect(within(ligne).getByText("S25")).toBeInTheDocument();
      await userEvent.click(within(ligne).getByRole("button", { name: "Effacer le peak" }));

      await waitFor(() => expect(api.mock.calls.some(([req]) => (req as Request).method === "DELETE")).toBe(true));
    });
  });
});
