import { fireEvent, screen } from "@testing-library/react";
import type { Schemas } from "@/api/client";
import { Fond } from "@/components/fond";
import { mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderComponent } from "@/test/render";

const clip = (id: number, overrides: Partial<Schemas["ClipResponse"]> = {}): Schemas["ClipResponse"] => ({
  id,
  gameId: id === 1 ? 42 : null,
  rattachement: null,
  auteurNom: id === 1 ? "Sam" : "Léa",
  auteurDiscordId: "111111111111111111",
  publieLe: "2026-10-17T22:30:00Z",
  nomFichier: `clip-${id}.mp4`,
  contentType: "video/mp4",
  taille: 100,
  masqueDuFond: false,
  soireeId: id === 42 ? 7 : null,
  scoreEquipeA: id === 42 ? 1 : null,
  scoreEquipeB: id === 42 ? 0 : null,
  ...overrides,
});

describe("Fond", () => {
  it("enchaîne les clips muets et crédite l'auteur et la Game", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips/fond": [clip(1), clip(2)] });
    const { container } = renderComponent(<Fond />);

    const video = await vi.waitFor(() => {
      const element = container.querySelector("video");
      if (!element) throw new Error("Fond vidéo pas encore chargé");
      return element;
    });
    expect(video).toHaveAttribute("src", "/api/clips/1/fichier");
    expect(video).toHaveProperty("muted", true);
    expect(video).toHaveAttribute("autoplay");
    expect(screen.getByText("Sam · Game #42")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Clip suivant" }));
    expect(await screen.findByText("Léa")).toBeInTheDocument();
    expect(container.querySelector("video")).toHaveAttribute("src", "/api/clips/2/fichier");
  });

  it("saute un clip illisible", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips/fond": [clip(1), clip(2)] });
    const { container } = renderComponent(<Fond />);

    const video = await vi.waitFor(() => {
      const element = container.querySelector("video");
      if (!element) throw new Error("Fond vidéo pas encore chargé");
      return element;
    });
    fireEvent.error(video);

    await vi.waitFor(() => expect(container.querySelector("video")).toHaveAttribute("src", "/api/clips/2/fichier"));
  });
});
