import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { ORIGINE_VIDE, player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Fiche = Schemas["FicheJoueur"];
type Ligne = Schemas["GameDeLaFiche"];

const statsVides = {
  gamesJouees: 0,
  victoires: 0,
  defaites: 0,
  winrate: null,
  kda: null,
  killsMoyens: null,
  mortsMoyennes: null,
  assistsMoyens: null,
  csParMinute: null,
  participationAuxKills: null,
  degatsMoyens: null,
  degatsParMinute: null,
  noteMoyenne: null,
  gamesNotees: 0,
} as Schemas["StatsDeCustoms"];

const ligne = (gameId: number, o: Partial<Ligne> = {}): Ligne => ({
  gameId,
  seriesId: 5,
  playedAt: "2026-10-17T20:00:00Z",
  equipe: "A",
  victoire: true,
  dureeSecondes: 1500,
  championId: "Camille",
  role: "Top",
  stats: { kills: 9, deaths: 2, assists: 7 } as Schemas["StatsDuParticipant"],
  performance: { note: 86, role: "Top", sourceDuRole: "Position", composantes: [] } as unknown as Schemas["NoteDePerformance"],
  dofd: false,
  place: 1,
  badge: null,
  participationAuxKills: null,
  adversaireChampionId: null,
  ...o,
});

const fiche = (o: Partial<Fiche> = {}): Fiche => ({
  playerId: 7,
  prenom: "Aldric",
  riotId: "Aldric#EUW",
  discord: { discordId: "1", username: "aldric", avatarUrl: null, principal: true, bannerUrl: null, bannerColor: null },
  comptesDiscord: [],
  cost: 4.4,
  soloQ: { tier: "DIAMOND", division: 2, lp: 45 },
  flex: null,
  stats: statsVides,
  champions: [],
  roles: [],
  games: [],
  titres: { dofd: 1 },
  joueAvec: [],
  resume: {
    games: 0,
    victoires: 0,
    defaites: 0,
    winrate: null,
    topChampions: [],
    kda: null,
    noteMoyenne: null,
    meilleuresPerfs: 0,
    aces: 0,
    dofd: 0,
  },
  liste: [],
  ...o,
});

const open = (reponse: Fiche | ((url: URL) => Fiche) = fiche()) => {
  const mock = mockApi({
    "GET /auth/me": user(),
    "GET /players/7": player({
      id: 7,
      prenom: "Aldric",
      riotId: "Aldric#EUW",
      mainRole: "TOP",
      currentCost: 4.4,
      rankTier: "DIAMOND",
      rankDivision: 2,
      lp: 45,
      peakTier: "EMERALD",
      peakDivision: 1,
      peakSeason: "S25",
      flexRankTier: "PLATINUM",
      flexRankDivision: 1,
      flexLp: 10,
    }),
    "GET /players/7/fiche": (req: Request) =>
      Response.json(typeof reponse === "function" ? reponse(new URL(req.url)) : reponse),
    "GET /players/7/cost/historique": { playerId: 7, mois: [] },
    "GET /champions": [
      { id: "Camille", key: "164", name: "Camille", imageUrl: "https://dd/camille.png" },
      { id: "Ahri", key: "103", name: "Ahri", imageUrl: "https://dd/ahri.png" },
    ],
    "GET /catalogue": {
      version: "15.1.1",
      objets: [{ id: 6655, name: "Rayon de Luden", iconUrl: "https://dd/item/6655.png" }],
      sorts: [{ id: 4, name: "Saut éclair", iconUrl: "https://dd/spell/Flash.png" }],
      arbres: [
        {
          id: 8100,
          name: "Domination",
          iconUrl: "https://dd/perk/8100.png",
          runes: [{ id: 8112, name: "Électrocution", iconUrl: "https://dd/perk/8112.png" }],
        },
      ],
      champions: [],
    },
  });
  renderApp("/joueurs/7");
  return mock;
};

describe("Fiche publique", () => {
  it("résume les 30 dernières games : winrate, top champions, KDA, note, badges et DOFD", async () => {
    open(
      fiche({
        resume: {
          games: 30,
          victoires: 20,
          defaites: 10,
          winrate: 66.7,
          topChampions: [{ championId: "Camille", games: 12, winrate: 75, kda: 3.3 }],
          kda: 2.8,
          noteMoyenne: 78,
          meilleuresPerfs: 4,
          aces: 2,
          dofd: 1,
        },
      }),
    );

    const resume = await screen.findByRole("region", { name: "30 dernières games" });
    expect(within(resume).getByRole("img", { name: /Winrate 67/ })).toBeInTheDocument();
    expect(within(resume).getByText("20 V – 10 D")).toBeInTheDocument();
    expect(within(resume).getByRole("listitem", { name: /Camille/ })).toHaveTextContent("75 %");
    expect(within(resume).getByText("2,8").parentElement).toHaveTextContent("KDA moyen");
    expect(within(resume).getByText("78").parentElement).toHaveTextContent("Note moyenne");
    expect(within(resume).getByText("MVP").parentElement).toHaveTextContent("× 4");
    expect(within(resume).getByText("ACE").parentElement).toHaveTextContent("× 2");
    expect(within(resume).getByText("DOFD").parentElement).toHaveTextContent("× 1");
  });

  it("montre le rôle principal dans l'en-tête et le rang Solo avec peak et Flex dans la colonne gauche", async () => {
    open();

    const rang = await screen.findByRole("region", { name: "Classé Solo" });
    expect(within(rang).getByText("Diamond II · 45 LP")).toBeInTheDocument();
    expect(within(rang).getByText("Emerald I · 0 LP")).toBeInTheDocument();
    expect(within(rang).getByText("Platinum I · 10 LP")).toBeInTheDocument();
    expect((await screen.findByText("principal")).parentElement).toHaveTextContent("Top principal");
  });

  it("n'affiche que les trois meilleurs champions jusqu'à « Tous les champions », et le « Joué avec »", async () => {
    const champ = (id: string, n: number) => ({
      championId: id,
      stats: { ...statsVides, gamesJouees: n, winrate: 50, kda: 2.5, csParMinute: 7.1, killsMoyens: 8, mortsMoyennes: 5, assistsMoyens: 5 },
    });
    open(
      fiche({
        champions: [champ("Camille", 9), champ("Garen", 8), champ("Zed", 7), champ("Ori", 2)],
        joueAvec: [{ playerId: 2, prenom: "Charles", parties: 18, victoires: 11, defaites: 7, winrate: 61.1 }],
      }),
    );

    const region = await screen.findByRole("region", { name: "Performance par champion" });
    expect(within(region).getAllByRole("listitem")).toHaveLength(3);
    await userEvent.click(within(region).getByRole("button", { name: "Tous les champions" }));
    expect(within(region).getAllByRole("listitem")).toHaveLength(4);

    const avec = screen.getByRole("region", { name: "Joué avec" });
    expect(within(avec).getByText("Charles")).toBeInTheDocument();
    expect(avec).toHaveTextContent("18 games");
    expect(avec).toHaveTextContent("11 V – 7 D");
  });

  it("liste les games groupées avec en-tête de groupe, note, place, badge, et ouvre la page de la game", async () => {
    open(
      fiche({
        liste: [
          {
            soireeId: 3,
            jour: null,
            games: [
              ligne(31, { badge: "MeilleurePerf", place: 1 }),
              ligne(30, { victoire: false, badge: "Ace", place: 2, performance: { note: 70 } as Schemas["NoteDePerformance"] }),
            ],
          },
        ],
      }),
    );

    const groupe = await screen.findByRole("group", { name: /Soirée du/ });
    const bilan = within(within(groupe).getByRole("list", { name: "Bilan" })).getAllByRole("listitem");
    expect(bilan.map((li) => li.textContent)).toEqual(["Note moy. 78", "1 victoire", "1 défaite"]);
    const lien = within(groupe).getByRole("link", { name: "Voir la game 31" });
    expect(lien).toHaveAttribute("href", "/games/31");
    expect(lien).toHaveTextContent("86");
    expect(lien).toHaveTextContent("1er");
    expect(lien).toHaveTextContent("MVP");
    expect(lien).toHaveTextContent("9 / 2 / 7");
    expect(within(groupe).getByRole("link", { name: "Voir la game 30" })).toHaveTextContent("ACE");
  });

  it("montre la ligne dense d'une game : résultat, série, durée, champion, sorts, runes, rôle, KDA, ratios, objets, adversaire et note", async () => {
    open(
      fiche({
        liste: [
          {
            soireeId: null,
            jour: null,
            games: [
              ligne(31, {
                badge: "MeilleurePerf",
                role: "Mid",
                dureeSecondes: 1500,
                adversaireChampionId: "Ahri",
                participationAuxKills: 86,
                stats: {
                  kills: 41,
                  deaths: 3,
                  assists: 3,
                  minionsKilled: 150,
                  neutralMinionsKilled: 25,
                  spell1Id: 4,
                  spell2Id: 4,
                  perks: [8112],
                  perkSubStyle: 8100,
                  items: [6655, null, null, null, null, null, 6655],
                } as unknown as Schemas["StatsDuParticipant"],
                performance: { note: 95 } as Schemas["NoteDePerformance"],
              }),
            ],
          },
        ],
      }),
    );

    const lien = await screen.findByRole("link", { name: "Voir la game 31" });
    expect(lien).toHaveTextContent("Victoire");
    expect(lien).not.toHaveTextContent("Série");
    expect(lien).toHaveTextContent("25:00");
    expect(lien).toHaveTextContent("41 / 3 / 3");
    expect(lien).toHaveTextContent("MVP");
    expect(lien).toHaveTextContent(/il y a \d+ j/);
    expect(lien).toHaveTextContent("Custom");
    expect(lien).toHaveTextContent("14,7 KDA");
    expect(lien).toHaveTextContent("7 CS/m");
    expect(lien).toHaveTextContent("86 % KP");
    expect(lien).toHaveTextContent("VS");
    expect(await within(lien).findByRole("img", { name: "Ahri" })).toHaveAttribute("src", "https://dd/ahri.png");
    expect(within(lien).getByRole("img", { name: "Mid" })).toBeInTheDocument();
    const anneau = within(lien).getByRole("img", { name: "Note 95 sur 100" });
    expect(anneau).toHaveTextContent("95");
    expect(lien).toHaveTextContent("1er");
    expect(lien.textContent?.match(/95/g)).toHaveLength(1);
    expect(lien.textContent?.match(/95/g)).toHaveLength(1);
    expect(await within(lien).findAllByRole("img", { name: "Saut éclair" })).toHaveLength(2);
    expect(within(lien).getByRole("img", { name: "Électrocution" })).toBeInTheDocument();
    expect(within(lien).getByRole("img", { name: "Domination" })).toBeInTheDocument();
    expect(within(within(lien).getByRole("list", { name: "Objets" })).getAllByRole("listitem")).toHaveLength(6);
  });

  it("masque ce que l'API ne donne pas plutôt que de l'inventer : adversaire, participation aux kills, durée", async () => {
    open(
      fiche({
        liste: [
          {
            soireeId: null,
            jour: null,
            games: [ligne(31, { adversaireChampionId: null, participationAuxKills: null, dureeSecondes: null, seriesId: null })],
          },
        ],
      }),
    );

    const lien = await screen.findByRole("link", { name: "Voir la game 31" });
    expect(lien).not.toHaveTextContent("VS");
    expect(lien).not.toHaveTextContent("KP");
    expect(lien).not.toHaveTextContent("CS/m");
    expect(lien).not.toHaveTextContent("Série");
    expect(lien).not.toHaveTextContent("NaN");
  });

  describe("ligne dépliable", () => {
    const participant = (playerId: number, prenom: string) => ({
      playerId,
      prenom,
      riotId: `${prenom}#EUW`,
      avatarUrl: null,
      rankTier: "GOLD",
      rankDivision: 2,
      cost: 3,
      championId: "Camille",
      stats: { kills: 5, deaths: 1, assists: 2, minionsKilled: 100, neutralMinionsKilled: 0 },
      performance: null,
      champsCorriges: [],
      details: null,
      place: 1,
      badge: null,
      impact: null,
      ...ORIGINE_VIDE,
    });
    const recap = {
      id: 31,
      seriesId: null,
      gameNumber: 1,
      bestOf: 1,
      status: "completed",
      playedAt: "2026-10-17T20:00:00Z",
      completedAt: null,
      modifieeParAdminLe: null,
      teamASide: "blue",
      teamBSide: "red",
      winner: "A",
      teamAWins: 1,
      teamBWins: 0,
      seriesOutcome: "Gagnee",
      riotMatchId: "EUW1_1",
      match: { durationSeconds: 1500, gameMode: "CLASSIC", gameType: "CUSTOM_GAME", gameVersion: null },
      teamAObjectifs: null,
      teamBObjectifs: null,
      chronologie: null,
      teamA: [participant(1, "Alice")],
      teamB: [participant(2, "Bob")],
    };
    const ouvrir = (game: () => Response) => {
      let appels = 0;
      const mock = mockApi({
        "GET /auth/me": user(),
        "GET /players/7": player({ id: 7, prenom: "Aldric" }),
        "GET /players/7/fiche": fiche({ liste: [{ soireeId: null, jour: null, games: [ligne(31)] }] }),
        "GET /players/7/cost/historique": { playerId: 7, mois: [] },
        "GET /champions": [],
        "GET /catalogue": { version: "1", objets: [], sorts: [], arbres: [], champions: [] },
        "GET /games/31": () => {
          appels++;
          return game();
        },
        "GET /games/31/votes": { dofd: { gagnants: [] } },
      });
      renderApp("/joueurs/7");
      return { mock, appels: () => appels };
    };

    it("ne charge la game qu'à l'ouverture, puis montre le scoreboard sans onglets des deux équipes", async () => {
      const { appels } = ouvrir(() => Response.json(recap));
      const deplier = await screen.findByRole("button", { name: "Déplier la game 31" });
      expect(deplier).toHaveAttribute("aria-expanded", "false");
      expect(appels()).toBe(0);

      await userEvent.click(deplier);

      expect(screen.getByRole("button", { name: "Replier la game 31" })).toHaveAttribute("aria-expanded", "true");
      expect(await screen.findByRole("list", { name: /Équipe A/ })).toHaveTextContent("Alice");
      expect(screen.queryByRole("tab")).not.toBeInTheDocument();
      expect(screen.getByRole("list", { name: /Équipe B/ })).toHaveTextContent("Bob");
      expect(screen.getByRole("link", { name: /Voir la game complète/ })).toHaveAttribute("href", "/games/31");
      expect(appels()).toBe(1);
      const panneau = screen.getByRole("list", { name: /Équipe A/ }).closest("div.bg-black\\/25");
      expect(panneau).not.toBeNull();
      expect(panneau).not.toContainElement(screen.getByRole("button", { name: "Replier la game 31" }));
    });

    it("ne navigue pas en dépliant, et replier masque le panneau", async () => {
      ouvrir(() => Response.json(recap));
      await userEvent.click(await screen.findByRole("button", { name: "Déplier la game 31" }));
      await screen.findByRole("list", { name: /Équipe A/ });
      expect(screen.getByRole("link", { name: "Voir la game 31" })).toBeInTheDocument();

      await userEvent.click(screen.getByRole("button", { name: "Replier la game 31" }));
      expect(screen.queryByRole("list", { name: /Équipe A/ })).not.toBeInTheDocument();
    });

    it("signale l'erreur de chargement tout en gardant le lien vers la game", async () => {
      ouvrir(() => Response.json({ title: "Boom" }, { status: 500 }));
      await userEvent.click(await screen.findByRole("button", { name: "Déplier la game 31" }));

      expect(await screen.findByRole("alert")).toBeInTheDocument();
      expect(screen.getByRole("link", { name: /Voir la game complète/ })).toHaveAttribute("href", "/games/31");
    });
  });

  it("affiche une ligne dédiée « Stats non saisies » pour une game sans stats, qui reste ouvrable", async () => {
    open(
      fiche({
        liste: [{ soireeId: null, jour: null, games: [ligne(31, { stats: null, performance: null, place: null, championId: null })] }],
      }),
    );

    const lien = await screen.findByRole("link", { name: "Voir la game 31" });
    expect(lien).toHaveTextContent("Stats non saisies");
    expect(lien).toHaveAttribute("href", "/games/31");
    expect(lien).not.toHaveTextContent("– / – / –");
  });

  it("relit la liste quand on filtre par rôle, regroupement, champion, série ou résultat, et Réinitialiser revient au départ", async () => {
    const urls: URL[] = [];
    open((url) => {
      urls.push(url);
      return fiche({ champions: [{ championId: "Camille", stats: statsVides }], games: [ligne(31), ligne(32, { seriesId: 9 })] });
    });
    const derniere = () => urls[urls.length - 1]!.searchParams;

    await screen.findByRole("group", { name: "Filtres de la liste" });
    expect(derniere().get("regroupement")).toBe("Soiree");

    await userEvent.click(screen.getByRole("button", { name: "Jungle" }));
    await waitFor(() => expect(derniere().get("role")).toBe("Jungle"));
    await userEvent.click(screen.getByRole("button", { name: "Jour" }));
    await waitFor(() => expect(derniere().get("regroupement")).toBe("Jour"));
    await userEvent.click(screen.getByRole("button", { name: "Aucun" }));
    await waitFor(() => expect(derniere().get("regroupement")).toBe("Plat"));
    await userEvent.selectOptions(screen.getByLabelText("Série"), "9");
    await waitFor(() => expect(derniere().get("serieId")).toBe("9"));
    await userEvent.selectOptions(screen.getByLabelText("Champion"), "Camille");
    await waitFor(() => expect(derniere().get("champion")).toBe("Camille"));
    await userEvent.selectOptions(screen.getByLabelText("Résultat"), "Defaite");
    await waitFor(() => expect(derniere().get("resultat")).toBe("Defaite"));

    await userEvent.click(screen.getByRole("button", { name: "Réinitialiser" }));
    expect(screen.getByRole("button", { name: "Tous" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "Soirée" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByLabelText("Série")).toHaveValue("");
    expect(screen.getByLabelText("Champion")).toHaveValue("");
    expect(screen.getByLabelText("Résultat")).toHaveValue("");
  });

  it("affiche des blocs vides sans erreur pour un joueur sans game", async () => {
    open();

    expect(await screen.findByText("Aucune game.")).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "30 dernières games" })).toHaveTextContent("–");
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});
