import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { mockApi, problem } from "@/test/api";
import { renderApp } from "@/test/render";

describe("Tutoriel", () => {
  it("s'ouvre sans connexion depuis la barre latérale", async () => {
    mockApi({ "GET /auth/me": problem(401) });
    renderApp("/");

    const nav = await screen.findByRole("navigation", { name: "Navigation principale" });
    await userEvent.click(within(nav).getByRole("link", { name: "Tutoriel" }));

    expect(await screen.findByRole("heading", { level: 1, name: "Tutoriel" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Connexion" })).not.toBeInTheDocument();
  });

  it("explique qui est quoi avec le vocabulaire du projet", async () => {
    mockApi({ "GET /auth/me": problem(401) });
    renderApp("/tutoriel");

    const sujet = await screen.findByRole("region", { name: "Qui est quoi" });
    for (const role of ["Joueur", "Capitaine", "Organisateur", "Administrateur", "Spectateur"]) {
      expect(within(sujet).getByText(role, { selector: "dt" })).toBeInTheDocument();
    }
  });

  it.each(["Companion", "LoL Proximity Chat"])("présente %s avec l'étiquette Bientôt et les trois blocs", async (titre) => {
    mockApi({ "GET /auth/me": problem(401) });
    renderApp("/tutoriel");

    const sujet = await screen.findByRole("region", { name: new RegExp(`^${titre}`) });
    expect(within(sujet).getByText("Bientôt")).toBeInTheDocument();
    for (const bloc of ["À quoi ça sert", "Pourquoi", "Comment ça marche"]) {
      expect(within(sujet).getByRole("heading", { name: bloc })).toBeInTheDocument();
    }
  });
});
