import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { json, mockApi, problem } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const players = [player({ id: 1, prenom: "Alice" }), player({ id: 2, prenom: "Bob", riotId: "Bob#EUW" })];

describe("Compte", () => {
  it("shows the Discord profile and links the account to a player", async () => {
    let linked: number | null = null;
    let linkBody: unknown;
    mockApi({
      "GET /auth/me": user({ username: "Thomas" }),
      "GET /players": players,
      "GET /players/me": () =>
        linked ? json(players.find((p) => p.id === linked)) : problem(404, "Aucun joueur n'est lié à ton compte Discord."),
      "POST /players/link": async (request) => {
        linkBody = await request.json();
        linked = 2;
        return json({ success: true });
      },
    });

    renderApp("/compte");

    expect(await screen.findByRole("heading", { name: "Thomas" })).toBeInTheDocument();
    expect(await screen.findByText("Aucun joueur n'est lié à ton compte Discord.")).toBeInTheDocument();

    await userEvent.type(screen.getByRole("searchbox", { name: "Chercher un joueur" }), "bo");
    expect(screen.queryByText("Alice#EUW")).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Lier Bob" }));

    expect(linkBody).toEqual({ playerId: 2 });
    const linkedPlayer = await screen.findByRole("region", { name: "Joueur lié" });
    expect(await within(linkedPlayer).findByText("Bob#EUW")).toBeInTheDocument();
  });

  it("shows the API error when the player is already linked to someone else", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players": players,
      "GET /players/me": problem(404),
      "POST /players/link": problem(409, "Ce joueur est déjà lié à un autre compte Discord. Demande à un admin."),
    });

    renderApp("/compte");

    await userEvent.click(await screen.findByRole("button", { name: "Lier Alice" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Ce joueur est déjà lié à un autre compte Discord. Demande à un admin.",
    );
  });
});
