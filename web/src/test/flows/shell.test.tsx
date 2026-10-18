import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { mockApi, problem } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const page = { "GET /players": [], "GET /soirees/open": { soiree: null } };

afterEach(() => {
  vi.unstubAllGlobals();
  localStorage.clear();
  document.documentElement.classList.add("dark");
});

describe("Sidebar", () => {
  it("lists the five entries in order and highlights the active page", async () => {
    mockApi({ "GET /auth/me": user(), ...page });
    renderApp("/joueurs");

    const nav = await screen.findByRole("navigation", { name: "Navigation principale" });
    const liens = within(nav).getAllByRole("link");
    expect(liens.map((l) => l.textContent)).toEqual(["Soirée", "Joueurs", "Classements", "Historique", "Clips"]);
    expect(liens.filter((l) => l.getAttribute("aria-current") === "page").map((l) => l.textContent)).toEqual(["Joueurs"]);
  });

  it("shows Administration to administrators only", async () => {
    mockApi({ "GET /auth/me": user({ isAdmin: true }), ...page });
    renderApp("/joueurs");
    const nav = await screen.findByRole("navigation", { name: "Navigation principale" });
    expect(await within(nav).findByRole("link", { name: "Administration" })).toBeInTheDocument();
  });

  it("hides Administration from other users", async () => {
    mockApi({ "GET /auth/me": user({ isAdmin: false }), ...page });
    renderApp("/joueurs");
    await screen.findByRole("navigation", { name: "Navigation principale" });
    await screen.findByRole("button", { name: /Thomas/ });
    expect(screen.queryByRole("link", { name: "Administration" })).not.toBeInTheDocument();
  });

  it("is absent on pages opened by an access link", async () => {
    mockApi({ "GET /auth/me": problem(401), "GET /liens/abc": problem(404) });
    renderApp("/draft/spectateur/abc");
    expect(await screen.findByRole("banner")).toBeInTheDocument();
    expect(screen.queryByRole("navigation", { name: "Navigation principale" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Ouvrir le menu" })).not.toBeInTheDocument();
  });
});

describe("Tiroir mobile", () => {
  it("opens with a button of at least 44 px and closes when a page is chosen", async () => {
    mockApi({ "GET /auth/me": user(), ...page });
    const { router } = renderApp("/joueurs");

    const bouton = await screen.findByRole("button", { name: "Ouvrir le menu" });
    expect(bouton).toHaveClass("size-11");
    await userEvent.click(bouton);

    const tiroir = await screen.findByRole("dialog");
    await userEvent.click(within(tiroir).getByRole("link", { name: "Historique" }));

    expect(router.state.location.pathname).toBe("/historique");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});

async function ouvrirLeMenu() {
  await userEvent.click(await screen.findByRole("button", { name: /Thomas/ }));
  return screen.findByRole("menu");
}

describe("Menu utilisateur", () => {
  it("shows the profile link, the theme row and a red logout under a separator", async () => {
    mockApi({ "GET /auth/me": user(), ...page });
    renderApp("/joueurs");

    const menu = await ouvrirLeMenu();
    expect(within(menu).getByRole("menuitem", { name: "Mon profil" })).toHaveAttribute("href", "/compte");
    expect(within(menu).getByRole("menuitem", { name: /Thème/ })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: "Se déconnecter" })).toHaveClass("text-destructive");
    expect(within(menu).getByRole("separator")).toBeInTheDocument();
  });

  it("goes to the profile page", async () => {
    mockApi({ "GET /auth/me": user(), ...page, "GET /players/me": { linked: false } });
    const { router } = renderApp("/joueurs");

    const menu = await ouvrirLeMenu();
    await userEvent.click(within(menu).getByRole("menuitem", { name: "Mon profil" }));

    expect(router.state.location.pathname).toBe("/compte");
  });
});

describe("Thème", () => {
  it("is dark by default, switches to light in one click and remembers the choice", async () => {
    mockApi({ "GET /auth/me": user(), ...page });
    const premier = renderApp("/joueurs");
    await screen.findByRole("navigation", { name: "Navigation principale" });
    expect(document.documentElement).toHaveClass("dark");

    const menu = await ouvrirLeMenu();
    await userEvent.click(within(menu).getByRole("menuitem", { name: "Thème Sombre" }));
    expect(document.documentElement).not.toHaveClass("dark");
    expect(screen.getByRole("menuitem", { name: "Thème Clair" })).toBeInTheDocument();

    premier.unmount();
    document.documentElement.classList.add("dark");
    renderApp("/joueurs");
    await screen.findByRole("navigation", { name: "Navigation principale" });
    expect(document.documentElement).not.toHaveClass("dark");
  });

  it("keeps working when the browser refuses storage", async () => {
    vi.stubGlobal("localStorage", {
      getItem: () => {
        throw new Error("blocked");
      },
      setItem: () => {
        throw new Error("blocked");
      },
    });
    mockApi({ "GET /auth/me": user(), ...page });
    renderApp("/joueurs");
    const menu = await ouvrirLeMenu();
    await userEvent.click(within(menu).getByRole("menuitem", { name: "Thème Sombre" }));
    expect(document.documentElement).not.toHaveClass("dark");
  });
});

describe("Tiroir mobile et menu utilisateur", () => {
  it("offers the same user menu in the drawer", async () => {
    mockApi({ "GET /auth/me": user({ isAdmin: true }), ...page });
    renderApp("/joueurs");

    await userEvent.click(await screen.findByRole("button", { name: "Ouvrir le menu" }));
    const tiroir = await screen.findByRole("dialog");
    expect(within(tiroir).getByRole("link", { name: "Administration" })).toBeInTheDocument();
    await userEvent.click(within(tiroir).getByRole("button", { name: /Thomas/ }));

    const menu = await screen.findByRole("menu");
    expect(within(menu).getByRole("menuitem", { name: "Se déconnecter" })).toBeInTheDocument();
  });
});
