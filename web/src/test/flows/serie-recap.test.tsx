import { screen, within } from "@testing-library/react";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Participant = Schemas["GameParticipantDto"];
type Series = Schemas["SeriesResponse"];

const participant = (playerId: number, prenom: string): Participant => ({
  playerId,
  prenom,
  riotId: `${prenom}#EUW`,
  avatarUrl: null,
  rankTier: null,
  rankDivision: null,
  cost: 3,
  role: null,
});

const game = (id: number, overrides: Partial<Schemas["SeriesGameResponse"]> = {}): Schemas["SeriesGameResponse"] => ({
  id,
  status: "completed",
  teamASide: "Blue",
  teamBSide: "Red",
  winner: "A",
  teamA: [],
  teamB: [],
  riotMatchId: null,
  championsJoues: [],
  rapportEnRevue: null,
  startedAt: null,
  ...overrides,
});

const serie = (overrides: Partial<Series> = {}): Series => ({
  seriesId: 5,
  soireeId: 7,
  format: 3,
  fearless: true,
  outcome: "EnCours",
  winningTeam: null,
  teamAWins: 1,
  teamBWins: 0,
  teamA: [participant(1, "Aldric"), participant(2, "Kuro")],
  teamB: [participant(3, "Mina"), participant(4, "Elio")],
  capitaineA: 2,
  capitaineB: 3,
  games: [game(21), game(22, { status: "pending", winner: null })],
  ...overrides,
});

const routes = (series: Series = serie()) => ({ "GET /auth/me": user(), "GET /series/5": series });

describe("Page de la Série", () => {
  it("montre format, options, score, Équipes et Games avec leur vainqueur", async () => {
    mockApi(routes());
    renderApp("/series/5");

    expect(await screen.findByRole("heading", { name: /Série BO3/ })).toBeInTheDocument();
    expect(screen.getByText("Fearless")).toBeInTheDocument();
    expect(screen.getByLabelText("Score")).toHaveTextContent("A 1–0 B");
    expect(within(screen.getByRole("group", { name: "Équipe A" })).getByText("Aldric")).toBeInTheDocument();
    const games = screen.getByRole("list", { name: "Games de la série" });
    expect(within(games).getByText("Équipe A gagne")).toBeInTheDocument();
    const liens = within(games).getAllByRole("link", { name: /Voir la game/ });
    expect(liens.map((l) => l.getAttribute("href"))).toEqual(["/games/21", "/games/22"]);
  });

  it("ne montre ni capitaine, ni couronne, ni lien, ni code, ni commande", async () => {
    mockApi(routes());
    renderApp("/series/5");

    await screen.findByRole("heading", { name: /Série BO3/ });
    expect(screen.queryByLabelText("Capitaine")).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Liens et code" })).not.toBeInTheDocument();
    expect(screen.queryByText(/Code Companion/)).not.toBeInTheDocument();
    for (const commande of [/Lancer la game/, /Victoire Équipe/, /Arrêter la série/, /Remplacer/, /Lancer la revanche/]) {
      expect(screen.queryByRole("button", { name: commande })).not.toBeInTheDocument();
    }
    expect(screen.queryByRole("heading", { name: "Remplacement" })).not.toBeInTheDocument();
  });

  it("offre le bouton « Espace organisateur » à l'organisateur connecté", async () => {
    mockApi(routes());
    renderApp("/series/5");

    const bouton = await screen.findByRole("link", { name: "Espace organisateur" });
    expect(bouton).toHaveAttribute("href", "/series/5/organisateur");
  });

  it("ne propose pas ce bouton sans session", async () => {
    mockApi({ "GET /auth/me": new Response(null, { status: 401 }), "GET /series/5": serie() });
    const { router } = renderApp("/series/5");

    await vi.waitFor(() => expect(router.state.location.pathname).toBe("/connexion"));
    expect(screen.queryByRole("link", { name: "Espace organisateur" })).not.toBeInTheDocument();
  });

  describe("rôles joués", () => {
    const joue = (playerId: number, prenom: string, role: Participant["role"]) => ({ ...participant(playerId, prenom), role });
    const champions = [
      { playerId: 1, championId: "Sion" },
      { playerId: 2, championId: "Zed" },
    ];

    it("affiche dans le cadre d'Équipe le rôle de la dernière game jouée, pas le rôle déclaré, du Top au Support", async () => {
      mockApi(
        routes(
          serie({
            teamA: [joue(1, "Aldric", "Top"), joue(2, "Kuro", "Support")],
            games: [
              game(21, {
                teamA: [joue(1, "Aldric", "Jungle"), joue(2, "Kuro", "Top")],
                championsJoues: champions,
              }),
              game(22, {
                teamA: [joue(2, "Kuro", "Mid"), joue(1, "Aldric", "Top")],
                championsJoues: champions,
              }),
            ],
          }),
        ),
      );
      renderApp("/series/5");

      const cadre = await screen.findByRole("group", { name: "Équipe A" });
      const lignes = within(cadre).getAllByRole("listitem");
      expect(lignes[0]).toHaveTextContent(/^Top.*Aldric/);
      expect(lignes[1]).toHaveTextContent(/^Mid.*Kuro/);
      expect(within(cadre).queryByText("Support")).not.toBeInTheDocument();
    });

    it("garde le rôle déclaré sans game jouée, ou sans rôle détecté dans la game", async () => {
      mockApi(
        routes(
          serie({
            teamA: [joue(1, "Aldric", "Top"), joue(2, "Kuro", "Support")],
            games: [game(21, { teamA: [joue(1, "Aldric", null), joue(2, "Kuro", null)] })],
          }),
        ),
      );
      renderApp("/series/5");

      const cadre = await screen.findByRole("group", { name: "Équipe A" });
      expect(within(cadre).getByText("Top")).toBeInTheDocument();
      expect(within(cadre).getByText("Support")).toBeInTheDocument();
    });

    it("ordonne la composition de chaque game par rôle joué", async () => {
      mockApi({
        ...routes(
          serie({
            games: [
              game(21, {
                teamA: [joue(1, "Aldric", "Support"), joue(2, "Kuro", "Top"), joue(5, "Zoe", "Mid")],
                championsJoues: [
                  { playerId: 1, championId: "Rakan" },
                  { playerId: 2, championId: "Sion" },
                  { playerId: 5, championId: "Zed" },
                ],
              }),
            ],
          }),
        ),
        "GET /champions": [],
      });
      renderApp("/series/5");

      const liste = await screen.findByRole("list", { name: "Champions joués par l'Équipe A" });
      const prenoms = within(liste).getAllByRole("listitem").slice(1).map((li) => li.textContent);
      expect(prenoms.map((t) => ["Kuro", "Zoe", "Aldric"].findIndex((n) => t?.includes(n)))).toEqual([0, 1, 2]);
    });
  });
});
