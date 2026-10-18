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
    expect(await screen.findByRole("link", { name: "Administration" })).toBeInTheDocument();
  });

  it("hides Administration from other users", async () => {
    mockApi({ "GET /auth/me": user({ isAdmin: false }), ...page });
    renderApp("/joueurs");
    await screen.findByRole("navigation", { name: "Navigation principale" });
    await screen.findByRole("link", { name: /Thomas/ });
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

describe("Thème", () => {
  it("is dark by default, switches to light and remembers the choice", async () => {
    mockApi({ "GET /auth/me": user(), ...page });
    const premier = renderApp("/joueurs");
    await screen.findByRole("navigation", { name: "Navigation principale" });
    expect(document.documentElement).toHaveClass("dark");

    await userEvent.click(screen.getByRole("button", { name: "Thème : Sombre" }));
    expect(document.documentElement).not.toHaveClass("dark");
    expect(screen.getByRole("button", { name: "Thème : Clair" })).toBeInTheDocument();

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
    await userEvent.click(await screen.findByRole("button", { name: "Thème : Sombre" }));
    expect(document.documentElement).not.toHaveClass("dark");
  });
});
