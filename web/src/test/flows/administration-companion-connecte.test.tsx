import { screen, within } from "@testing-library/react";
import { mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const base = {
  "GET /auth/me": user({ isAdmin: true }),
  "GET /games/pending": [],
  "GET /players": [],
  "GET /series/active": { series: [] },
};

describe("Administration : Companion connecté", () => {
  it("liste les Companions connectés avec leur Joueur, leur Série et « vu il y a X s »", async () => {
    mockApi({
      ...base,
      "GET /companion/connectes": [
        { seriesId: 5, joueur: { id: 3, prenom: "Camille", riotId: "Camille#EUW" }, vuIlYASecondes: 12 },
        { seriesId: 5, joueur: null, vuIlYASecondes: 47 },
      ],
    });
    renderApp("/administration?onglet=serie-en-cours");

    const section = await screen.findByRole("region", { name: "Companion connecté" });
    const camille = (await within(section).findByText("Camille")).closest("li") as HTMLElement;
    expect(within(camille).getByText(/Série 5/)).toBeInTheDocument();
    expect(within(camille).getByText("vu il y a 12 s")).toBeInTheDocument();
    const inconnu = within(section).getByText("Joueur inconnu").closest("li") as HTMLElement;
    expect(within(inconnu).getByText("vu il y a 47 s")).toBeInTheDocument();
    expect(within(section).queryByText("aucun Companion détecté")).not.toBeInTheDocument();
  });

  it("annonce « aucun Companion détecté » quand personne n'est connecté", async () => {
    mockApi({ ...base, "GET /companion/connectes": [] });
    renderApp("/administration?onglet=serie-en-cours");

    const section = await screen.findByRole("region", { name: "Companion connecté" });
    expect(await within(section).findByText("aucun Companion détecté")).toBeInTheDocument();
  });
});
