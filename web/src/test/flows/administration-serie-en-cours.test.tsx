import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const serie = (overrides: Partial<Schemas["SeriesResponse"]> = {}): Schemas["SeriesResponse"] => ({
  seriesId: 5,
  soireeId: 1,
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

const liens: Schemas["LiensSerieResponse"] = {
  seriesId: 5,
  lienCapitaineA: "secret-a",
  lienCapitaineB: "secret-b",
  lienSpectateur: "secret-spectateur",
  echecDmA: null,
  echecDmB: null,
  capitaineARejointLe: "2026-10-19T18:30:00Z",
  capitaineBRejointLe: null,
  capitaineAPretLe: "2026-10-19T18:31:00Z",
  capitaineBPretLe: null,
};

const base = { "GET /games/pending": [], "GET /players": [] };

const avecSerie = (isAdmin: boolean) => ({
  ...base,
  "GET /auth/me": user({ isAdmin }),
  "GET /series/active": { series: [serie()] },
  "GET /series/5/liens": liens,
  "GET /series/5/code-companion": { seriesId: 5, code: "code-companion-1" },
});

describe("Administration : série en cours", () => {
  it("montre à l'Administrateur le code, les liens et l'état des Capitaines", async () => {
    mockApi(avecSerie(true));
    renderApp("/administration?onglet=serie-en-cours");

    expect(await screen.findByLabelText("Code Companion")).toHaveValue("code-companion-1");
    expect(screen.getByLabelText("Lien capitaine de l'Équipe A")).toHaveValue(
      `${window.location.origin}/draft/capitaine/secret-a`,
    );
    expect(screen.getByLabelText("Lien capitaine de l'Équipe B")).toHaveValue(
      `${window.location.origin}/draft/capitaine/secret-b`,
    );
    expect(screen.getByLabelText("Lien spectateur")).toHaveValue(
      `${window.location.origin}/draft/spectateur/secret-spectateur`,
    );
    expect(screen.getByLabelText("État du Capitaine de l'Équipe A")).toHaveTextContent(/rejoint à/i);
    expect(screen.getByLabelText("Prêt du Capitaine de l'Équipe A")).toHaveTextContent(/prêt à/i);
    expect(screen.getByLabelText("État du Capitaine de l'Équipe B")).toHaveTextContent(/pas encore rejoint/i);
    expect(screen.getByLabelText("Prêt du Capitaine de l'Équipe B")).toHaveTextContent(/pas encore prêt/i);
    // Lecture seule : le renouvellement reste à l'organisateur.
    expect(screen.queryByRole("button", { name: /renouveler/i })).not.toBeInTheDocument();
  });

  it("copie un lien", async () => {
    const utilisateur = userEvent.setup();
    mockApi(avecSerie(true));
    renderApp("/administration?onglet=serie-en-cours");

    const champ = (await screen.findByLabelText("Lien spectateur")).parentElement!;
    await utilisateur.click(within(champ).getByRole("button", { name: /copier/i }));

    expect(await within(champ).findByText("Copié")).toBeInTheDocument();
  });

  it("dit qu'aucune série n'est en cours", async () => {
    mockApi({ ...base, "GET /auth/me": user({ isAdmin: true }), "GET /series/active": { series: [] } });
    renderApp("/administration?onglet=serie-en-cours");

    expect(await screen.findByText(/aucune série en cours/i)).toBeInTheDocument();
  });

  it("ne demande ni code ni liens à un utilisateur non Administrateur", async () => {
    const api = mockApi(avecSerie(false));
    renderApp("/administration?onglet=serie-en-cours");

    expect(await screen.findByText(/réservée aux administrateurs/i)).toBeInTheDocument();
    expect(screen.queryByLabelText("Code Companion")).not.toBeInTheDocument();
    expect(api.mock.calls.some(([req]) => /liens|code-companion/.test((req as Request).url))).toBe(false);
  });
});
