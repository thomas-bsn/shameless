import { screen, within } from "@testing-library/react";
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
});
