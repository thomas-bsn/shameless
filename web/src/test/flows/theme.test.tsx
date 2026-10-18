import { cleanup, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { THEME_STORAGE_KEY } from "@/lib/theme";
import { mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const apiDeLaPage = {
  "GET /auth/me": user(),
  "GET /health": new Response("Healthy"),
  "GET /soirees/open": { soiree: null },
};

describe("Thème", () => {
  it("affiche le thème sombre par défaut et l'applique à <html>", async () => {
    mockApi(apiDeLaPage);

    renderApp("/");

    expect(await screen.findByRole("button", { name: "Sombre" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "Clair" })).toHaveAttribute("aria-pressed", "false");
    expect(document.documentElement).toHaveClass("dark");
  });

  it("mémorise le choix du thème clair et le réapplique au chargement suivant", async () => {
    mockApi(apiDeLaPage);

    renderApp("/");
    await userEvent.click(await screen.findByRole("button", { name: "Clair" }));

    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe("light");
    expect(document.documentElement).not.toHaveClass("dark");

    cleanup();
    renderApp("/");

    expect(await screen.findByRole("button", { name: "Clair" })).toHaveAttribute("aria-pressed", "true");
    expect(document.documentElement).not.toHaveClass("dark");
  });
});
