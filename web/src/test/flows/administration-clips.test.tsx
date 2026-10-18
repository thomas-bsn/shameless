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

const calme = {
  "GET /auth/me": user({ isAdmin: true }),
  "GET /games/pending": [],
  "GET /series/active": { series: [] },
  "GET /players": [],
};

const liste = (clips: () => Clip[]) => () => json({ items: clips(), total: clips().length, page: 1, taille: 50 });

describe("Administration : onglet Clips du Fond", () => {
  it("liste tous les clips avec leur état et laisse masquer puis réafficher un clip", async () => {
    let clips = [clip({ id: 1, nomFichier: "pentakill.mp4" }), clip({ id: 2, nomFichier: "baron.mp4", masqueDuFond: true })];
    const api = mockApi({
      ...calme,
      "GET /clips": liste(() => clips),
      "PUT /clips/1/masque-du-fond": () => {
        clips = [clip({ id: 1, masqueDuFond: true }), clips[1]];
        return json({ id: 1, masqueDuFond: true });
      },
      "DELETE /clips/1/masque-du-fond": () => {
        clips = [clip({ id: 1 }), clips[1]];
        return json({ id: 1, masqueDuFond: false });
      },
    });
    renderApp("/administration?onglet=clips");

    const premier = (await screen.findByText("pentakill.mp4")).closest("li") as HTMLElement;
    const second = screen.getByText("baron.mp4").closest("li") as HTMLElement;
    expect(within(premier).getByText("Dans le Fond")).toBeInTheDocument();
    expect(within(second).getByText("Masqué")).toBeInTheDocument();

    await userEvent.click(within(premier).getByRole("switch", { name: "Clip dans le Fond" }));
    await vi.waitFor(() => expect(api.mock.calls.some(([r]) => r.method === "PUT")).toBe(true));
    expect(await within(premier).findByText("Masqué")).toBeInTheDocument();

    await userEvent.click(within(premier).getByRole("switch", { name: "Clip dans le Fond" }));
    await vi.waitFor(() => expect(api.mock.calls.some(([r]) => r.method === "DELETE")).toBe(true));
    expect(await within(premier).findByText("Dans le Fond")).toBeInTheDocument();
  });

  it("n'affiche que les clips masqués avec le filtre « Masqués »", async () => {
    mockApi({
      ...calme,
      "GET /clips": liste(() => [clip({ id: 1, nomFichier: "pentakill.mp4" }), clip({ id: 2, nomFichier: "baron.mp4", masqueDuFond: true })]),
    });
    renderApp("/administration?onglet=clips");

    await screen.findByText("pentakill.mp4");
    await userEvent.click(screen.getByRole("switch", { name: "Masqués" }));

    expect(screen.queryByText("pentakill.mp4")).not.toBeInTheDocument();
    expect(screen.getByText("baron.mp4")).toBeInTheDocument();
  });
});
