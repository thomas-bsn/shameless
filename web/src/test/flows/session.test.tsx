import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { loginUrl } from "@/lib/session";
import { mockApi, problem } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

describe("Session", () => {
  it("sends a visitor opening a protected page to the login, which comes back to that page", async () => {
    mockApi({ "GET /auth/me": problem(401) });

    const { router } = renderApp("/historique?annee=2026");

    const login = await screen.findByRole("link", { name: "Se connecter avec Discord" });
    expect(router.state.location.pathname).toBe("/connexion");
    expect(login).toHaveAttribute("href", loginUrl("/historique?annee=2026"));
  });

  it("offers a login button that comes back to the current page", async () => {
    mockApi({ "GET /auth/me": problem(401), "GET /health": new Response("Healthy") });

    renderApp("/");

    expect(await screen.findByRole("link", { name: "Se connecter" })).toHaveAttribute("href", loginUrl("/"));
  });

  it("shows the signed-in user and logs them out", async () => {
    let signedIn = true;
    const fetch = mockApi({
      "GET /auth/me": () => (signedIn ? Response.json(user({ username: "Thomas" })) : problem(401)),
      "POST /auth/logout": () => {
        signedIn = false;
        return new Response(null, { status: 204 });
      },
      "GET /health": new Response("Healthy"),
    });

    renderApp("/");

    expect(await screen.findByRole("link", { name: /Thomas/ })).toHaveAttribute("href", "/compte");
    await userEvent.click(screen.getByRole("button", { name: "Déconnexion" }));

    expect(await screen.findByRole("link", { name: "Se connecter" })).toBeInTheDocument();
    await waitFor(() =>
      expect(fetch.mock.calls.some(([request]) => request.method === "POST" && request.url.endsWith("/api/auth/logout"))).toBe(true),
    );
  });
});

describe("Session expirée", () => {
  it("sends the user back to the login when the API stops recognising the session", async () => {
    mockApi({ "GET /auth/me": user(), "GET /players": problem(401), "GET /players/me": problem(401) });

    const { router } = renderApp("/compte");

    expect(await screen.findByRole("link", { name: "Se connecter avec Discord" })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/connexion");
  });
});
