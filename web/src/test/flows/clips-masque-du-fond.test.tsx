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

const liste = (clips: () => Clip[]) => () => json({ items: clips(), total: clips().length, page: 1, taille: 50 });

describe("Masquer un clip du Fond", () => {
  it("laisse un Administrateur masquer puis réafficher un clip depuis le lecteur", async () => {
    let clips = [clip()];
    const api = mockApi({
      "GET /auth/me": user({ isAdmin: true }),
      "GET /clips": liste(() => clips),
      "PUT /clips/1/masque-du-fond": () => {
        clips = [clip({ masqueDuFond: true })];
        return json({ id: 1, masqueDuFond: true });
      },
      "DELETE /clips/1/masque-du-fond": () => {
        clips = [clip({ masqueDuFond: false })];
        return json({ id: 1, masqueDuFond: false });
      },
    });

    renderApp("/clips");
    await userEvent.click(await screen.findByRole("button", { name: "Lire le clip pentakill.mp4" }));
    const lecteur = await screen.findByRole("dialog");
    const interrupteur = await within(lecteur).findByRole("switch", { name: "Clip dans le Fond" });
    expect(interrupteur).toBeChecked();
    expect(within(lecteur).getByText("Dans le Fond")).toBeInTheDocument();

    await userEvent.click(interrupteur);
    await vi.waitFor(() => expect(api.mock.calls.some(([r]) => r.method === "PUT")).toBe(true));
    expect(await within(lecteur).findByText("Masqué")).toBeInTheDocument();
    expect(within(lecteur).getByRole("switch", { name: "Clip dans le Fond" })).not.toBeChecked();

    await userEvent.click(within(lecteur).getByRole("switch", { name: "Clip dans le Fond" }));
    await vi.waitFor(() => expect(api.mock.calls.some(([r]) => r.method === "DELETE")).toBe(true));
    expect(await within(lecteur).findByText("Dans le Fond")).toBeInTheDocument();
  });

  it("n'affiche pas l'interrupteur à un Utilisateur qui n'est pas Administrateur", async () => {
    mockApi({
      "GET /auth/me": user({ isAdmin: false }),
      "GET /clips": liste(() => [clip({ masqueDuFond: true })]),
    });

    renderApp("/clips");
    await userEvent.click(await screen.findByRole("button", { name: "Lire le clip pentakill.mp4" }));
    const lecteur = await screen.findByRole("dialog");

    expect(within(lecteur).queryByRole("switch")).not.toBeInTheDocument();
  });
});
