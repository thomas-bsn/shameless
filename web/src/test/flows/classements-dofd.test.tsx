import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const ligne = (rang: number, playerId: number, prenom: string, dofd: number) => ({
  rang,
  playerId,
  prenom,
  riotId: `${prenom}#EUW`,
  dofd,
});

const hall: Schemas["HallDeLaHonte"] = {
  joueurs: [ligne(1, 5, "Tao", 7), ligne(2, 2, "Ana", 5), ligne(2, 1, "Zed", 5), ligne(4, 3, "Bob", 3), ligne(5, 4, "Lou", 2)],
};

describe("Classements, vue DOFD", () => {
  it("affiche le podium puis la liste, les ex æquo au même rang et le rang suivant sauté", async () => {
    mockApi({ "GET /auth/me": user(), "GET /hall-de-la-honte": hall, "GET /soirees/open": { soiree: null } });
    renderApp("/classements?vue=dofd");

    const podium = within(await screen.findByRole("list", { name: "Podium des DOFD" })).getAllByRole("listitem");
    expect(podium.map((m) => m.getAttribute("aria-label"))).toEqual(["2e ex æquo : Ana", "1er : Tao", "2e ex æquo : Zed"]);
    expect(podium[1]).toHaveTextContent("7");
    expect(within(podium[1]!).getByRole("link", { name: "Tao" })).toHaveAttribute("href", "/joueurs/5");
    expect(within(podium[1]!).getByRole("img", { name: "Bouse" })).toBeInTheDocument();

    const lignes = within(screen.getByRole("list", { name: "Classement des DOFD" })).getAllByRole("listitem");
    expect(lignes.map((l) => within(l).getByLabelText("Rang").textContent)).toEqual(["4", "5"]);
    expect(lignes[0]).toHaveTextContent("Bob");
    expect(lignes[0]).toHaveTextContent("3 DOFD");
  });

  it("marque les ex æquo de la liste", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /hall-de-la-honte": {
        joueurs: [ligne(1, 1, "A", 4), ligne(2, 2, "B", 3), ligne(3, 3, "C", 2), ligne(4, 4, "D", 1), ligne(4, 5, "E", 1)],
      },
    });
    renderApp("/classements?vue=dofd");

    const lignes = within(await screen.findByRole("list", { name: "Classement des DOFD" })).getAllByRole("listitem");
    expect(lignes).toHaveLength(2);
    expect(lignes[0]).toHaveTextContent("ex æquo");
    expect(lignes[1]).toHaveTextContent("ex æquo");
  });

  it("annonce un classement vide tant qu'aucun DOFD n'est élu", async () => {
    mockApi({ "GET /auth/me": user(), "GET /hall-de-la-honte": { joueurs: [] } });
    renderApp("/classements?vue=dofd");

    expect(await screen.findByText(/Aucun DOFD élu pour l'instant/)).toBeInTheDocument();
  });

  it("est atteignable par l'onglet DOFD, gardé dans l'adresse", async () => {
    mockApi({ "GET /auth/me": user(), "GET /players": [], "GET /hall-de-la-honte": hall });
    const { router } = renderApp("/classements");

    await userEvent.click(await screen.findByRole("tab", { name: "DOFD" }));
    await waitFor(() => expect(router.state.location.search).toEqual({ vue: "dofd" }));
    expect(await screen.findByRole("list", { name: "Podium des DOFD" })).toBeInTheDocument();
  });

  it("redirige l'ancienne adresse du Hall de la Honte vers la vue DOFD", async () => {
    mockApi({ "GET /auth/me": user(), "GET /hall-de-la-honte": hall });
    const { router } = renderApp("/hall-de-la-honte");

    expect(await screen.findByRole("list", { name: "Podium des DOFD" })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/classements");
    expect(router.state.location.search).toEqual({ vue: "dofd" });
  });
});
