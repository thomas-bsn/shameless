import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { mockApi, problem } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const soiree: Schemas["OpenSoireeResponse"] = {
  soiree: { id: 7, status: "Open", openedAt: "2026-10-16T19:00:00Z", closedAt: null, picker: null },
};

const players = [
  player({ id: 1, prenom: "Alice", riotId: "Alice#EUW" }),
  player({ id: 2, prenom: "Bob", riotId: "Bob#EUW" }),
  player({ id: 3, prenom: "Chloé", riotId: "Chloe#EUW" }),
];

const coche = (prenom: RegExp) => screen.getByRole("checkbox", { name: prenom });

describe("Picker : candidats vocaux", () => {
  it("coche les Joueurs liés présents en vocal Discord", async () => {
    const candidats: Schemas["CandidatsVocaux"] = {
      channelId: "900",
      joueurs: [
        { playerId: 1, prenom: "Alice", discordId: "101" },
        { playerId: 3, prenom: "Chloé", discordId: "103" },
      ],
      membresNonLies: 2,
    };
    mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": soiree,
      "GET /players": players,
      "GET /soirees/open/picker/candidats-vocaux": candidats,
    });
    renderApp("/soiree/picker");

    await userEvent.click(await screen.findByRole("button", { name: "Ajouter les joueurs en vocal" }));

    expect(await screen.findByText(/2 joueurs en vocal ajoutés/)).toBeInTheDocument();
    expect(screen.getByText(/2 membres en vocal sans joueur lié/)).toBeInTheDocument();
    expect(coche(/Alice/)).toBeChecked();
    expect(coche(/Bob/)).not.toBeChecked();
    expect(coche(/Chloé/)).toBeChecked();
    expect(screen.getByText("2/10")).toBeInTheDocument();
  });

  it("affiche la raison quand le bot est indisponible", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": soiree,
      "GET /players": players,
      "GET /soirees/open/picker/candidats-vocaux": problem(503, "Le bot Discord ne répond pas : réessayez dans un instant."),
    });
    renderApp("/soiree/picker");

    await userEvent.click(await screen.findByRole("button", { name: "Ajouter les joueurs en vocal" }));

    expect(await screen.findByText("Le bot Discord ne répond pas : réessayez dans un instant.")).toBeInTheDocument();
    expect(coche(/Alice/)).not.toBeChecked();
  });
});
