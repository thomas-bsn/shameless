import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { json, mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const serie = (overrides: Partial<Schemas["SeriesResponse"]> = {}): Schemas["SeriesResponse"] => ({
  seriesId: 11,
  soireeId: 7,
  format: 3,
  fearless: false,
  outcome: "EnCours",
  winningTeam: null,
  teamAWins: 0,
  teamBWins: 0,
  teamA: [],
  teamB: [],
  capitaineA: null,
  capitaineB: null,
  games: [],
  ...overrides,
});

describe("Side automatique", () => {
  it("lance la game sans demander de Side ni envoyer de choix", async () => {
    const fetch = mockApi({
      "GET /auth/me": user(),
      "GET /series/11": serie(),
      "POST /series/11/games": json({}, 201),
    });
    renderApp("/series/11");

    const lancer = await screen.findByRole("button", { name: "Lancer la game 1" });
    expect(screen.queryByLabelText("Side de l'Équipe A")).not.toBeInTheDocument();
    expect(screen.queryByText("Alternance automatique")).not.toBeInTheDocument();
    await userEvent.click(lancer);

    await waitFor(() => {
      const appel = fetch.mock.calls.find(([r]) => r.method === "POST" && r.url.endsWith("/api/series/11/games"));
      expect(appel).toBeDefined();
    });
    const [requete] = fetch.mock.calls.find(([r]) => r.method === "POST" && r.url.endsWith("/api/series/11/games"))!;
    expect(await requete.clone().text()).toBe("");
  });

  it("n'affiche plus d'option de Sides alternés sur la série", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /series/11": serie(),
    });
    renderApp("/series/11");

    await screen.findByRole("button", { name: "Lancer la game 1" });
    expect(screen.queryByText("Sides alternés")).not.toBeInTheDocument();
  });
});
