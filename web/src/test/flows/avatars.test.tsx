import { screen, within } from "@testing-library/react";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const PHOTO = "https://cdn.discordapp.com/avatars/1/alice.png";

describe("Avatars des joueurs", () => {
  it("montre la photo Discord d'un joueur lié et ses initiales sinon, dans le tableau des Joueurs", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players": [
        player({ id: 1, prenom: "Alice", riotId: "Alice#EUW", avatarUrl: PHOTO }),
        player({ id: 2, prenom: "Bob", riotId: "Bob#EUW", avatarUrl: null }),
      ],
      "GET /classements/5v5": { joueurs: [] },
    });
    renderApp("/joueurs");

    const photo = await screen.findByRole("img", { name: "Photo de Alice" });
    expect(photo).toHaveAttribute("src", PHOTO);
    expect(screen.queryByRole("img", { name: "Photo de Bob" })).not.toBeInTheDocument();
    const ligneBob = screen.getByRole("link", { name: /Bob/ }).closest("tr")!;
    expect(ligneBob).toHaveTextContent("BO");
  });

  it("l'affiche sur le podium des Classements", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players": [
        player({ id: 1, prenom: "Alice", currentCost: 9, avatarUrl: PHOTO }),
        player({ id: 2, prenom: "Bob", currentCost: 5, avatarUrl: null }),
      ],
      "GET /soirees/open": { soiree: null },
    });
    renderApp("/classements");

    const podium = await screen.findByRole("list", { name: "Podium du cost" });
    expect(within(podium).getByRole("img", { name: "Photo de Alice" })).toHaveAttribute("src", PHOTO);
    expect(within(podium).queryByRole("img", { name: "Photo de Bob" })).not.toBeInTheDocument();
  });

  it("l'affiche sur la fiche du joueur", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players/7": player({ id: 7, prenom: "Aldric", riotId: "Aldric#EUW", avatarUrl: PHOTO }),
    });
    renderApp("/joueurs/7");

    expect(await screen.findByRole("img", { name: "Photo de Aldric" })).toHaveAttribute("src", PHOTO);
  });

  const participant = (playerId: number, prenom: string, avatarUrl: string | null): Schemas["GameParticipantDto"] => ({
    playerId,
    prenom,
    riotId: `${prenom}#EUW`,
    rankTier: null,
    rankDivision: null,
    cost: 3,
    role: null,
    avatarUrl,
  });

  it("l'affiche sur la page de la Série", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /series/5": {
        seriesId: 5,
        soireeId: 7,
        format: 3,
        fearless: false,
        outcome: "EnCours",
        winningTeam: null,
        teamAWins: 0,
        teamBWins: 0,
        teamA: [participant(1, "Aldric", PHOTO), participant(2, "Kuro", null)],
        teamB: [],
        capitaineA: null,
        capitaineB: null,
        games: [],
      },
    });
    renderApp("/series/5");

    const equipe = await screen.findByRole("group", { name: "Équipe A" });
    expect(within(equipe).getByRole("img", { name: "Photo de Aldric" })).toHaveAttribute("src", PHOTO);
    expect(within(equipe).queryByRole("img", { name: "Photo de Kuro" })).not.toBeInTheDocument();
    expect(equipe).toHaveTextContent("KU");
  });

  it("l'affiche sur la draft de champions d'un Lien spectateur, sans connexion", async () => {
    mockApi({
      "GET /champions": [],
      "GET /liens/lien-spec": { seriesId: 5, portee: "Spectateur", equipe: null },
      "GET /liens/lien-spec/draft-champions": {
        gameId: 12,
        seriesId: 5,
        fearless: false,
        equipeBlue: "A",
        equipeRed: "B",
        terminee: false,
        tourCourant: null,
        tours: [],
        actions: [],
        teamA: [participant(1, "Aldric", PHOTO)],
        teamB: [participant(3, "Mina", null)],
        capitaineA: 1,
        capitaineB: 3,
        championsDejaJoues: [],
      },
    });
    renderApp("/draft/spectateur/lien-spec");

    expect(await screen.findByRole("img", { name: "Photo de Aldric" })).toHaveAttribute("src", PHOTO);
    expect(screen.queryByRole("img", { name: "Photo de Mina" })).not.toBeInTheDocument();
  });
});
