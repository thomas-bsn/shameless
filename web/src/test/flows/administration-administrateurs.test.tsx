import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { json, mockApi, problem } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const moi = () => user({ isAdmin: true, role: "Administrateur", discordUserId: "1001", username: "Thomas" });

const utilisateur = (overrides: Partial<Schemas["UtilisateurResponse"]> = {}): Schemas["UtilisateurResponse"] => ({
  discordUserId: "2002",
  username: "Camille",
  avatarUrl: null,
  role: "Utilisateur",
  promotedBy: null,
  promotedAt: null,
  ...overrides,
});

const calme = { "GET /games/pending": [], "GET /series/active": { series: [] }, "GET /players": [] };

const ligne = async (pseudo: string) => (await within(await screen.findByRole("table")).findByText(pseudo)).closest("tr") as HTMLElement;

describe("Administration : onglet Administrateurs", () => {
  it("liste les Utilisateurs avec leur rôle et la mention de promotion", async () => {
    mockApi({
      "GET /auth/me": moi(),
      ...calme,
      "GET /utilisateurs": [
        utilisateur({ discordUserId: "1001", username: "Thomas", role: "Administrateur", promotedBy: "Sam", promotedAt: "2026-10-18T10:00:00Z" }),
        utilisateur({ discordUserId: "3003", username: "Sam", role: "Superadministrateur" }),
        utilisateur(),
      ],
    });
    renderApp("/administration?onglet=administrateurs");

    expect(within(await ligne("Sam")).getByText("Superadmin")).toBeInTheDocument();
    const admin = await ligne("Thomas");
    expect(within(admin).getByText("Admin")).toBeInTheDocument();
    expect(within(admin).getByText(/promu par Sam le 18\/10\/2026/)).toBeInTheDocument();
    const simple = await ligne("Camille");
    expect(within(simple).queryByText("Admin")).not.toBeInTheDocument();
    expect(within(simple).queryByText(/promu par/)).not.toBeInTheDocument();
  });

  it("filtre la liste par pseudo", async () => {
    mockApi({
      "GET /auth/me": moi(),
      ...calme,
      "GET /utilisateurs": [utilisateur(), utilisateur({ discordUserId: "4004", username: "Bastien" })],
    });
    renderApp("/administration?onglet=administrateurs");

    await screen.findByText("Camille");
    await userEvent.type(screen.getByRole("searchbox", { name: "Rechercher un utilisateur" }), "bast");

    expect(screen.getByText("Bastien")).toBeInTheDocument();
    expect(screen.queryByText("Camille")).not.toBeInTheDocument();
  });

  it("promeut un Utilisateur en Administrateur", async () => {
    let liste = [utilisateur()];
    const api = mockApi({
      "GET /auth/me": moi(),
      ...calme,
      "GET /utilisateurs": () => json(liste),
      "PUT /utilisateurs/2002/administrateur": () => {
        liste = [utilisateur({ role: "Administrateur", promotedBy: "Thomas", promotedAt: "2026-10-19T09:00:00Z" })];
        return json(liste[0]);
      },
    });
    renderApp("/administration?onglet=administrateurs");

    await userEvent.click(within(await ligne("Camille")).getByRole("button", { name: "Promouvoir Camille" }));

    const apres = await ligne("Camille");
    expect(await within(apres).findByText("Admin")).toBeInTheDocument();
    expect(within(apres).getByText(/promu par Thomas le 19\/10\/2026/)).toBeInTheDocument();
    expect(api.mock.calls.some(([req]) => (req as Request).method === "PUT")).toBe(true);
  });

  it("retire le rôle à un autre Administrateur", async () => {
    let liste = [utilisateur({ role: "Administrateur", promotedBy: "Thomas", promotedAt: "2026-10-19T09:00:00Z" })];
    const api = mockApi({
      "GET /auth/me": moi(),
      ...calme,
      "GET /utilisateurs": () => json(liste),
      "DELETE /utilisateurs/2002/administrateur": () => {
        liste = [utilisateur()];
        return json(liste[0]);
      },
    });
    renderApp("/administration?onglet=administrateurs");

    await userEvent.click(within(await ligne("Camille")).getByRole("button", { name: "Retirer le rôle de Camille" }));

    expect(await within(await ligne("Camille")).findByRole("button", { name: "Promouvoir Camille" })).toBeInTheDocument();
    expect(api.mock.calls.some(([req]) => (req as Request).method === "DELETE")).toBe(true);
  });

  it("ne permet de toucher ni un Superadministrateur ni soi-même, et l'explique", async () => {
    mockApi({
      "GET /auth/me": moi(),
      ...calme,
      "GET /utilisateurs": [
        utilisateur({ discordUserId: "1001", username: "Thomas", role: "Administrateur" }),
        utilisateur({ discordUserId: "3003", username: "Sam", role: "Superadministrateur" }),
      ],
    });
    renderApp("/administration?onglet=administrateurs");

    const sam = await ligne("Sam");
    expect(within(sam).queryByRole("button")).not.toBeInTheDocument();
    expect(within(sam).getByText(/défini en ligne de commande/i)).toBeInTheDocument();
    const thomas = await ligne("Thomas");
    expect(within(thomas).queryByRole("button")).not.toBeInTheDocument();
    expect(within(thomas).getByText(/ne peut pas se retirer/i)).toBeInTheDocument();
  });

  it("affiche l'erreur de l'API quand l'action est refusée", async () => {
    mockApi({
      "GET /auth/me": moi(),
      ...calme,
      "GET /utilisateurs": [utilisateur()],
      "PUT /utilisateurs/2002/administrateur": problem(404, "Cet utilisateur ne s'est jamais connecté à Shameless."),
    });
    renderApp("/administration?onglet=administrateurs");

    await userEvent.click(within(await ligne("Camille")).getByRole("button", { name: "Promouvoir Camille" }));

    expect(await screen.findByText(/ne s'est jamais connecté/)).toBeInTheDocument();
  });
});
