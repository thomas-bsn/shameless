import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { mockApi, problem } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const apiDesClips = { "GET /clips/sans-game": [] };

describe("Sidebar", () => {
  it("montre les cinq entrées dans l'ordre, la page active surlignée", async () => {
    mockApi({ "GET /auth/me": user(), ...apiDesClips });

    renderApp("/clips");

    const nav = await screen.findByRole("navigation", { name: "Navigation principale" });
    expect(within(nav).getAllByRole("link").map((lien) => lien.textContent)).toEqual([
      "Soirée",
      "Joueurs",
      "Classements",
      "Historique",
      "Clips",
    ]);
    expect(within(nav).getByRole("link", { name: "Clips" })).toHaveAttribute("aria-current", "page");
    expect(within(nav).getByRole("link", { name: "Soirée" })).not.toHaveAttribute("aria-current");
    expect(within(nav).queryByRole("link", { name: "Hall de la Honte" })).not.toBeInTheDocument();
  });

  it("garde la page active surlignée dans les sous-pages", async () => {
    mockApi({ "GET /auth/me": user() });

    renderApp("/joueurs/nouveau");

    const nav = await screen.findByRole("navigation", { name: "Navigation principale" });
    expect(within(nav).getByRole("link", { name: "Joueurs" })).toHaveAttribute("aria-current", "page");
  });

  it("cache Administration à qui n'est pas Administrateur", async () => {
    mockApi({ "GET /auth/me": user(), ...apiDesClips });

    renderApp("/clips");

    expect(await screen.findByRole("navigation", { name: "Navigation principale" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Administration" })).not.toBeInTheDocument();
  });

  it("montre Administration à un Administrateur et ouvre sa page", async () => {
    mockApi({ "GET /auth/me": user({ isAdmin: true }), ...apiDesClips });

    renderApp("/clips");

    const administration = await screen.findByRole("link", { name: "Administration" });
    expect(administration).toHaveAttribute("href", "/administration");

    await userEvent.click(administration);

    expect(await screen.findByRole("heading", { name: "Administration" })).toBeInTheDocument();
  });

  it("ouvre le tiroir au bouton de menu et le referme au choix d'une page", async () => {
    mockApi({ "GET /auth/me": user(), ...apiDesClips, "GET /soirees/open": { soiree: null } });

    const { router } = renderApp("/clips");

    const bouton = await screen.findByRole("button", { name: "Ouvrir le menu" });
    // `cible-tactile` fixe la cible tactile minimale : 44 px (index.css).
    expect(bouton).toHaveClass("cible-tactile");
    expect(bouton).toHaveAttribute("aria-expanded", "false");

    await userEvent.click(bouton);

    expect(await screen.findByRole("button", { name: "Fermer le menu" })).toHaveAttribute("aria-expanded", "true");

    await userEvent.click(screen.getByRole("link", { name: "Soirée" }));

    await waitFor(() => expect(router.state.location.pathname).toBe("/soiree"));
    expect(await screen.findByRole("button", { name: "Ouvrir le menu" })).toHaveAttribute("aria-expanded", "false");
  });

  it("n'affiche ni sidebar ni session sur une page ouverte par un lien d'accès", async () => {
    const fetch = mockApi({ "GET /auth/me": user(), "GET /liens/lien-cap": problem(404) });

    renderApp("/draft/spectateur/lien-cap");

    expect(await screen.findByText("Ce lien n'est pas valide")).toBeInTheDocument();
    expect(within(screen.getByRole("banner")).getByText("Shameless")).toBeInTheDocument();
    expect(screen.queryByRole("navigation", { name: "Navigation principale" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Ouvrir le menu" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Se connecter" })).not.toBeInTheDocument();
    expect(fetch.mock.calls.some(([requete]) => requete.url.endsWith("/api/auth/me"))).toBe(false);
  });
});
