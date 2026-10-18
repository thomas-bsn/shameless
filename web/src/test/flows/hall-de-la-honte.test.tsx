import { screen, within } from "@testing-library/react";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const hall: Schemas["HallDeLaHonte"] = {
  joueurs: [
    { rang: 1, playerId: 2, prenom: "Ana", riotId: "Ana#EUW", dofd: 3 },
    { rang: 1, playerId: 1, prenom: "Zed", riotId: "Zed#EUW", dofd: 3 },
    { rang: 3, playerId: 3, prenom: "Bob", riotId: "Bob#EUW", dofd: 1 },
  ],
};

describe("Hall de la Honte", () => {
  it("redirige l'ancienne adresse vers la vue DOFD et conserve les ex æquo", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips/fond": [], "GET /players": [], "GET /hall-de-la-honte": hall });

    const { router } = renderApp("/hall-de-la-honte");

    const classement = await screen.findByRole("list", { name: "Classement DOFD" });
    expect(router.state.location.pathname).toBe("/classements");
    expect(router.state.location.search).toEqual({ vue: "dofd" });
    const lignes = within(classement).getAllByRole("listitem");
    expect(lignes.map((l) => l.firstElementChild?.textContent)).toEqual(["1", "1", "3"]);
    expect(within(lignes[0]!).getByRole("link", { name: "Ana" })).toHaveAttribute("href", "/joueurs/2");
    expect(lignes[0]).toHaveTextContent("3");
    expect(lignes[2]).toHaveTextContent("Bob");
    expect(lignes[2]).toHaveTextContent("1");
  });

  it("annonce un Hall vide tant qu'aucun DOFD n'est élu", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips/fond": [], "GET /players": [], "GET /hall-de-la-honte": { joueurs: [] } });
    renderApp("/hall-de-la-honte");

    expect(await screen.findByText(/Aucun DOFD élu pour l'instant/)).toBeInTheDocument();
  });
});
