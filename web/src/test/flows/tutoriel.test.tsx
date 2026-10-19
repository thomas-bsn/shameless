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

  it.each(["LoL Proximity Chat"])("présente %s avec l'étiquette Bientôt et les trois blocs", async (titre) => {
    mockApi({ "GET /auth/me": problem(401) });
    renderApp("/tutoriel");

    const sujet = await screen.findByRole("region", { name: new RegExp(`^${titre}`) });
    expect(within(sujet).getByText("Bientôt")).toBeInTheDocument();
    for (const bloc of ["À quoi ça sert", "Pourquoi", "Comment ça marche"]) {
      expect(within(sujet).getByRole("heading", { name: bloc })).toBeInTheDocument();
    }
  });

  it("explique comment lancer une game, sans étiquette Bientôt, avec le vrai parcours", async () => {
    mockApi({ "GET /auth/me": problem(401) });
    renderApp("/tutoriel");

    const sujet = await screen.findByRole("region", { name: "Lancer une game" });
    expect(within(sujet).queryByText("Bientôt")).not.toBeInTheDocument();
    for (const bloc of ["À quoi ça sert", "Pourquoi", "Comment ça marche"]) {
      expect(within(sujet).getByRole("heading", { name: bloc })).toBeInTheDocument();
    }
    for (const mot of [/Picker/, /Capitaines/, /prêt/, /Reset/, /pause/, /automatiquement/, /chaque game/, /rien à lancer/]) {
      expect(sujet).toHaveTextContent(mot);
    }
  });

  it("explique le Companion : installation Windows, avertissement, connexion, état prêt et procédure d'échec", async () => {
    mockApi({ "GET /auth/me": problem(401) });
    renderApp("/tutoriel");

    const sujet = await screen.findByRole("region", { name: "Companion" });
    expect(within(sujet).queryByText("Bientôt")).not.toBeInTheDocument();
    for (const bloc of ["À quoi ça sert", "Pourquoi", "Comment ça marche"]) {
      expect(within(sujet).getByRole("heading", { name: bloc })).toBeInTheDocument();
    }
    for (const mot of [
      /Windows 10 ou 11 seulement/,
      /éditeur inconnu/,
      /Informations complémentaires/,
      /Exécuter quand même/,
      /adresse de l'Instance/,
      /Code Companion/,
      /client LoL détecté/,
      /Série reconnue/,
      /customs 5v5/,
      /Fichier de Rapport/,
      /Superadministrateur ou Administrateur/,
      /importe/,
    ]) {
      expect(sujet).toHaveTextContent(mot);
    }
    expect(within(sujet).getByRole("link", { name: /Télécharger le Companion/ })).toHaveAttribute(
      "href",
      "https://github.com/thomas-bsn/shameless/releases/latest/download/shameless-companion.exe",
    );
  });
});
