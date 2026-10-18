import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { mockApi, json } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

describe("Administration", () => {
  it("refuse clairement l'accès aux non-Administrateurs", async () => {
    mockApi({ "GET /auth/me": user() });
    renderApp("/administration");
    expect(await screen.findByRole("alert")).toHaveTextContent("Cette page est réservée aux Administrateurs.");
  });

  it("résout une Game sans résultat et interrompt une Série après confirmation", async () => {
    let pending = true;
    let active = true;
    vi.stubGlobal("confirm", vi.fn(() => true));
    const api = mockApi({
      "GET /auth/me": user({ isAdmin: true }),
      "GET /games/pending": () => json(pending ? [{ id: 31, playedAt: "2026-10-16T20:00:00Z", seriesId: 7, status: "pending", participants: [{ playerId: 1, side: "blue", equipe: "A", prenom: "Alice" }] }] : []),
      "GET /series/active": () => json({ series: active ? [{ seriesId: 7, outcome: "EnCours", teamAWins: 0, teamBWins: 0, teamA: [], teamB: [], games: [] }] : [] }),
      "GET /players": [],
      "PUT /games/31/result": () => { pending = false; return json({}); },
      "POST /series/7/stop": () => { active = false; return json({}); },
    });
    renderApp("/administration");
    expect(await screen.findByRole("heading", { name: "À traiter" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Équipe A gagne" }));
    await vi.waitFor(() => expect(api.mock.calls.some(([request]) => request.method === "PUT" && request.url.endsWith("/games/31/result"))).toBe(true));
    await userEvent.click(await screen.findByRole("button", { name: "Interrompre" }));
    await vi.waitFor(() => expect(api.mock.calls.some(([request]) => request.method === "POST" && request.url.endsWith("/series/7/stop"))).toBe(true));
    await vi.waitFor(() => expect(screen.queryByRole("heading", { name: "À traiter" })).not.toBeInTheDocument());
  });

  it("enregistre le cost et renseigne puis efface le Peak", async () => {
    const api = mockApi({
      "GET /auth/me": user({ isAdmin: true }), "GET /games/pending": [], "GET /series/active": { series: [] },
      "GET /players": [player()],
      "GET /players/1/cost": { playerId: 1, name: "Alice", mois: "2026-10-01", cost: 3, tier: "B", costDeDepart: 3, sourceDuCostDeDepart: "RangSoloQ", gamesTerminees: 0, victoires: 0, winrate: null, gamesNotees: 0, noteMoyenne: null, evolue: false, gamesMinimum: 5, ajustementPerformance: 0, ajustementResultats: 0 },
      "PUT /players/1/cost": () => json({}), "POST /players/1/peak": () => json({}), "DELETE /players/1/peak": () => json({}),
    });
    renderApp("/administration");
    await userEvent.click(await screen.findByRole("button", { name: "Enregistrer" }));
    const peak = screen.getByLabelText("Peak de Alice");
    await userEvent.type(peak, "DIAMOND");
    await userEvent.tab();
    await userEvent.click(screen.getByRole("button", { name: "Effacer peak" }));
    await vi.waitFor(() => expect(api.mock.calls.filter(([request]) => ["PUT", "POST", "DELETE"].includes(request.method))).toHaveLength(3));
    const requests = api.mock.calls.map(([request]) => request);
    expect(await requests.find((request) => request.method === "PUT")?.clone().json()).toEqual({ cost: 3 });
    expect(await requests.find((request) => request.method === "POST")?.clone().json()).toEqual({ peakTier: "DIAMOND", peakDivision: null, peakSeason: "", peakLp: 0 });
    expect(requests.some((request) => request.method === "DELETE" && request.url.endsWith("/players/1/peak"))).toBe(true);
  });

  it("montre les clips du Fond, filtre les masqués et appelle l'action pour les réafficher", async () => {
    let masked = true;
    const api = mockApi({
      "GET /auth/me": user({ isAdmin: true }), "GET /games/pending": [], "GET /series/active": { series: [] }, "GET /players": [],
      "GET /clips": (request) => {
        const params = new URL(request.url).searchParams;
        const clip = (id: number, nomFichier: string, masqueDuFond: boolean) => ({ id, gameId: null, rattachement: null, auteurNom: id === 9 ? "Sam" : "Test", auteurDiscordId: "1", publieLe: "2026-10-16T20:00:00Z", nomFichier, contentType: "video/mp4", taille: 3, masqueDuFond });
        const list = params.get("masques") === "true" ? (masked ? [clip(9, "masque.mp4", true)] : []) : Array.from({ length: 30 }, (_, i) => clip(i + 1, i === 8 ? "masque.mp4" : `clip-${i}.mp4`, i === 8 && masked));
        return json(params.get("page") === "2" ? [clip(31, "page-2.mp4", false)] : list);
      },
      "PUT /clips/9/fond": () => { masked = false; return json({}); },
    });
    renderApp("/administration");
    await userEvent.click(await screen.findByRole("tab", { name: "Clips du Fond" }));
    expect(await screen.findByText("Sam · #9")).toBeInTheDocument();
    expect(screen.getByLabelText("Aperçu masque.mp4")).toHaveAttribute("src", "/api/clips/9/fichier");
    expect(screen.getByRole("button", { name: "Réafficher" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Suivant" }));
    expect(await screen.findByText("Page 2")).toBeInTheDocument();
    expect(api.mock.calls.some(([request]) => new URL(request.url).searchParams.get("page") === "2")).toBe(true);
    await userEvent.click(screen.getByRole("button", { name: "Précédent" }));
    await userEvent.click(screen.getByLabelText("Masqués seulement"));
    expect(api.mock.calls.some(([request]) => new URL(request.url).searchParams.get("masques") === "true")).toBe(true);
    await userEvent.click(await screen.findByRole("button", { name: "Réafficher" }));
    await vi.waitFor(() => expect(api.mock.calls.some(([request]) => request.method === "PUT" && request.url.endsWith("/clips/9/fond"))).toBe(true));
  });
});
