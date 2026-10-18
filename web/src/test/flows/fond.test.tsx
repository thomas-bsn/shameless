import { fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { mockApi, problem } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const page = { "GET /players": [], "GET /soirees/open": { soiree: null } };

const clips = [
  { id: 11, auteurNom: "Mina", gameId: 14 },
  { id: 12, auteurNom: "Elio", gameId: null },
  { id: 13, auteurNom: "Tao", gameId: 3 },
];

/** jsdom n'a pas matchMedia : on simule l'écran étroit et la réduction des animations. */
function stubEcran({ etroit = false, reduit = false } = {}) {
  vi.stubGlobal("matchMedia", (query: string) => ({
    matches: query.includes("reduced-motion") ? reduit : query.includes("max-width") ? etroit : false,
    media: query,
    addEventListener: () => {},
    removeEventListener: () => {},
  }));
}

const video = () => document.querySelector("video");
const decor = () => screen.queryByTestId("fond-fixe");

beforeEach(() => stubEcran());

describe("Fond", () => {
  it("plays the first clip muted, credits its author and Game, and loads a single video", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips/fond": clips, ...page });
    renderApp("/joueurs");

    await screen.findByText("Mina");
    expect(video()).toHaveAttribute("src", "/api/clips/11/fichier");
    expect(video()?.muted).toBe(true);
    expect(document.querySelectorAll("video")).toHaveLength(1);
    expect(screen.getByRole("link", { name: "Game 14" })).toBeInTheDocument();
  });

  it("moves to the next clip with « Suivant »", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips/fond": clips, ...page });
    renderApp("/joueurs");

    await userEvent.click(await screen.findByRole("button", { name: "Suivant" }));

    expect(video()).toHaveAttribute("src", "/api/clips/12/fichier");
    expect(screen.getByText("Elio")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /^Game/ })).not.toBeInTheDocument();
  });

  it("chains the next clip when one ends, and loops back after the last", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips/fond": clips.slice(0, 2), ...page });
    renderApp("/joueurs");
    await screen.findByText("Mina");

    fireEvent.ended(video()!);
    await screen.findByText("Elio");
    fireEvent.ended(video()!);
    await screen.findByText("Mina");
  });

  it("skips an unreadable clip without showing an error", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips/fond": clips, ...page });
    renderApp("/joueurs");
    await screen.findByText("Mina");

    fireEvent.error(video()!);

    await screen.findByText("Elio");
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("falls back to the fixed backdrop when every clip fails", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips/fond": clips.slice(0, 2), ...page });
    renderApp("/joueurs");
    await screen.findByText("Mina");

    fireEvent.error(video()!);
    await screen.findByText("Elio");
    fireEvent.error(video()!);

    await waitFor(() => expect(video()).toBeNull());
    expect(decor()).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Suivant" })).not.toBeInTheDocument();
  });

  it("shows the fixed backdrop when the Instance has no clip", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips/fond": [], ...page });
    renderApp("/joueurs");

    expect(await screen.findByTestId("fond-fixe")).toBeInTheDocument();
    expect(video()).toBeNull();
    expect(screen.queryByRole("button", { name: "Suivant" })).not.toBeInTheDocument();
  });

  it("shows the fixed backdrop on a narrow screen, without fetching clips", async () => {
    stubEcran({ etroit: true });
    const fetchMock = mockApi({ "GET /auth/me": user(), "GET /clips/fond": clips, ...page });
    renderApp("/joueurs");

    expect(await screen.findByTestId("fond-fixe")).toBeInTheDocument();
    expect(video()).toBeNull();
    expect(fetchMock.mock.calls.some(([req]) => new URL(req.url).pathname === "/api/clips/fond")).toBe(false);
  });

  it("shows the fixed backdrop when the user asks for reduced motion", async () => {
    stubEcran({ reduit: true });
    mockApi({ "GET /auth/me": user(), "GET /clips/fond": clips, ...page });
    renderApp("/joueurs");

    expect(await screen.findByTestId("fond-fixe")).toBeInTheDocument();
    expect(video()).toBeNull();
  });

  it("keeps the fixed backdrop on pages without a session and does not request clips", async () => {
    const fetchMock = mockApi({ "GET /auth/me": problem(401), "GET /clips/fond": clips });
    renderApp("/connexion");

    expect(await screen.findByTestId("fond-fixe")).toBeInTheDocument();
    expect(video()).toBeNull();
    expect(fetchMock.mock.calls.some(([req]) => new URL(req.url).pathname === "/api/clips/fond")).toBe(false);
  });
});
