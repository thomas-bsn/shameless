import { screen } from "@testing-library/react";
import type { Schemas } from "@/api/client";
import { mockApi, problem } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const ouverte: Schemas["OpenSoireeResponse"] = {
  soiree: { id: 7, status: "Open", openedAt: "2026-10-16T19:00:00Z", closedAt: null, picker: null },
};

describe("Accueil", () => {
  it("shows the open Soirée to a signed-in user, with a link to it", async () => {
    mockApi({ "GET /auth/me": user(), "GET /health": new Response("Healthy"), "GET /soirees/open": ouverte });

    renderApp("/");

    expect(await screen.findByText("Une Soirée est en cours.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Aller à la Soirée" })).toHaveAttribute("href", "/soiree");
  });

  it("invites a signed-in user to open a Soirée when none is open", async () => {
    mockApi({ "GET /auth/me": user(), "GET /health": new Response("Healthy"), "GET /soirees/open": { soiree: null } });

    renderApp("/");

    expect(await screen.findByText("Aucune Soirée n'est ouverte.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Ouvrir une Soirée" })).toHaveAttribute("href", "/soiree");
  });

  it("does not ask a visitor's Soirée", async () => {
    const fetch = mockApi({ "GET /auth/me": problem(401), "GET /health": new Response("Healthy") });

    renderApp("/");

    expect(await screen.findByRole("link", { name: "Se connecter" })).toBeInTheDocument();
    expect(fetch.mock.calls.some(([r]) => r.url.endsWith("/api/soirees/open"))).toBe(false);
  });
});
