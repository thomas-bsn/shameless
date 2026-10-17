import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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
  it("liste le cumul des DOFD, les ex æquo au même rang, depuis la navigation", async () => {
    mockApi({ "GET /auth/me": user(), "GET /hall-de-la-honte": hall, "GET /soirees/open": { soiree: null } });
    renderApp("/");

    await userEvent.click(await screen.findByRole("link", { name: "Hall de la Honte" }));

    const lignes = within(await screen.findByRole("list", { name: "Hall de la Honte" })).getAllByRole("listitem");
    expect(lignes.map((l) => within(l).getByLabelText("Rang").textContent)).toEqual(["1", "1", "3"]);
    expect(within(lignes[0]!).getByRole("link", { name: /Ana/ })).toHaveAttribute("href", "/joueurs/2");
    expect(lignes[0]).toHaveTextContent("3 DOFD");
    expect(lignes[2]).toHaveTextContent("Bob");
    expect(lignes[2]).toHaveTextContent("1 DOFD");
  });

  it("annonce un Hall vide tant qu'aucun DOFD n'est élu", async () => {
    mockApi({ "GET /auth/me": user(), "GET /hall-de-la-honte": { joueurs: [] } });
    renderApp("/hall-de-la-honte");

    expect(await screen.findByText(/Aucun DOFD élu pour l'instant/)).toBeInTheDocument();
  });
});
