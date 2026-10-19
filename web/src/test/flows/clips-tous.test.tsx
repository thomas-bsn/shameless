import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { json, mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Clip = Schemas["ClipDeLaListe"];

const clip = (overrides: Partial<Clip> = {}): Clip => ({
  id: 1,
  gameId: null,
  rattachement: null,
  auteurNom: "Sam",
  auteurDiscordId: "111111111111111111",
  publieLe: "2026-10-17T13:30:00Z",
  nomFichier: "pentakill.mp4",
  contentType: "video/mp4",
  taille: 12_000_000,
  masqueDuFond: false,
  game: null,
  ...overrides,
});

const clips = [
  clip({ id: 1, auteurNom: "Sam", nomFichier: "pentakill.mp4" }),
  clip({ id: 2, auteurNom: "Elio", nomFichier: "baron.mp4", masqueDuFond: true, publieLe: "2026-10-10T20:00:00Z" }),
];

const liste = (req: Request) => {
  const q = new URL(req.url).searchParams;
  const auteur = q.get("auteur")?.toLowerCase();
  const items = clips.filter((c) => !auteur || c.auteurNom.toLowerCase().includes(auteur));
  return json({ items, total: items.length, page: 1, taille: 50 });
};

describe("Clips : onglet Tous", () => {
  it("liste tous les clips à plat, avec ou sans game, dans un onglet à part", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips": liste });
    renderApp("/clips");

    await userEvent.click(await screen.findByRole("tab", { name: "Tous" }));

    expect(await screen.findByRole("button", { name: "Lire le clip pentakill.mp4" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Lire le clip baron.mp4" })).toBeInTheDocument();
  });

  it("affiche une miniature vidéo (première image) pour chaque clip, l'icône n'étant qu'un repli", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips": liste });
    renderApp("/clips?vue=tous");

    const bouton = await screen.findByRole("button", { name: "Lire le clip pentakill.mp4" });
    const video = bouton.querySelector("video");
    expect(video).not.toBeNull();
    expect(video).toHaveAttribute("preload", "metadata");
    expect(video?.getAttribute("src")).toBe("/api/clips/1/fichier#t=0.001");
    expect(video?.muted).toBe(true);
    expect(video).toHaveAttribute("playsinline");
  });

  it("câble les filtres auteur et soirée sur GET /clips", async () => {
    const api = mockApi({ "GET /auth/me": user(), "GET /clips": liste });
    renderApp("/clips?vue=tous");

    await screen.findByRole("button", { name: "Lire le clip pentakill.mp4" });
    await userEvent.type(screen.getByLabelText("Auteur"), "eli");
    await vi.waitFor(() => expect(screen.queryByRole("button", { name: "Lire le clip pentakill.mp4" })).not.toBeInTheDocument());
    expect(screen.getByRole("button", { name: "Lire le clip baron.mp4" })).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText("Soirée"), "2026-10-10");
    await vi.waitFor(() =>
      expect(
        api.mock.calls.some(([r]) => {
          const q = new URL(r.url).searchParams;
          return q.get("auteur") === "eli" && q.get("soiree") === "2026-10-10";
        }),
      ).toBe(true),
    );
  });

  it("indique l'état « Masqué » sur les vignettes aux Administrateurs seulement", async () => {
    mockApi({ "GET /auth/me": user({ isAdmin: true }), "GET /clips": liste });
    renderApp("/clips?vue=tous");

    const masque = await screen.findByRole("button", { name: "Lire le clip baron.mp4" });
    expect(within(masque).getByText("Masqué")).toBeInTheDocument();
    expect(within(screen.getByRole("button", { name: "Lire le clip pentakill.mp4" })).queryByText("Masqué")).not.toBeInTheDocument();
  });

  it("ne montre pas l'état « Masqué » à un Utilisateur qui n'est pas Administrateur", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips": liste });
    renderApp("/clips?vue=tous");

    await screen.findByRole("button", { name: "Lire le clip baron.mp4" });
    expect(screen.queryByText("Masqué")).not.toBeInTheDocument();
  });
});
