import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { mockApi } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const api = () =>
  mockApi({
    "GET /auth/me": user(),
    "GET /players": [player({ id: 7, prenom: "Aldric", riotId: "Aldric#EUW", rankTier: "DIAMOND", rankDivision: 2 })],
    "GET /players/7": player({ id: 7, prenom: "Aldric", riotId: "Aldric#EUW" }),
    "GET /soirees/open": { soiree: null },
  });

describe("« ← Retour » de la fiche joueur", () => {
  it("ramène au Classement sur sa vue SoloQ quand la fiche en vient", async () => {
    api();
    const { router } = renderApp("/classements?vue=soloq");

    await userEvent.click(await screen.findByRole("link", { name: /Aldric/ }));
    await userEvent.click(await screen.findByRole("button", { name: "← Retour" }));

    await vi.waitFor(() => expect(router.state.location.pathname).toBe("/classements"));
    expect(router.state.location.search).toEqual({ vue: "soloq" });
    await vi.waitFor(() => expect(router.state.status).toBe("idle"));
  });

  it("ramène à la liste des Joueurs quand la fiche est ouverte directement", async () => {
    api();
    const { router } = renderApp("/joueurs/7");

    await userEvent.click(await screen.findByRole("button", { name: "← Retour" }));

    await vi.waitFor(() => expect(router.state.location.pathname).toBe("/joueurs"));
    await vi.waitFor(() => expect(router.state.status).toBe("idle"));
  });
});
