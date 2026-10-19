import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { json, mockApi, problem } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const etat = (
  overrides: Partial<Schemas["EtatActualisationRiot"]> = {},
): Schemas["EtatActualisationRiot"] => ({
  disponible: true,
  enCours: false,
  nature: null,
  total: 0,
  termines: 0,
  echecs: [],
  demarreeLe: null,
  termineeLe: null,
  ...overrides,
});

const base = {
  "GET /auth/me": user({ isAdmin: true }),
  "GET /games/pending": [],
  "GET /series/active": { series: [] },
  "GET /players": [player({ id: 1, prenom: "Alice" })],
  "GET /players/1/cost": {
    playerId: 1,
    name: "Alice",
    mois: "2026-10-01",
    cost: 3,
    tier: "C",
    costDeDepart: 3,
    sourceDuCostDeDepart: "RangSoloQ",
    gamesTerminees: 0,
    victoires: 0,
    winrate: null,
    gamesNotees: 0,
    noteMoyenne: null,
    evolue: false,
    gamesMinimum: 5,
    ajustementPerformance: 0,
    ajustementResultats: 0,
  },
};

describe("Administration : actualisation Riot", () => {
  it("grise les boutons et explique pourquoi quand il n'y a pas de clé Riot", async () => {
    mockApi({ ...base, "GET /admin/riot": etat({ disponible: false }) });
    renderApp("/administration");

    expect(await screen.findByText(/aucune clé riot/i)).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Tout actualiser" }),
    ).toBeDisabled();
    expect(
      screen.getByRole("button", { name: "Actualiser Alice" }),
    ).toBeDisabled();
  });

  it("actualise un joueur et signale l'échec Riot en gardant l'ancien rôle", async () => {
    const api = mockApi({
      ...base,
      "GET /admin/riot": etat(),
      "POST /admin/riot/joueurs/1": json({
        playerId: 1,
        prenom: "Alice",
        rangsActualises: true,
        roleActualise: false,
        echecs: [
          "Rôle principal : Riot n'a pas répondu, l'ancien rôle est conservé. Cause : clé Riot refusée (403), elle est invalide ou expirée.",
        ],
        causeRangs: null,
        causeRole: "clé Riot refusée (403), elle est invalide ou expirée",
      }),
    });
    renderApp("/administration");

    const ligne = (await screen.findByText("Alice")).closest(
      "tr",
    ) as HTMLElement;
    await userEvent.click(
      within(ligne).getByRole("button", { name: "Actualiser Alice" }),
    );

    expect(
      await within(ligne).findByText(/l'ancien rôle est conservé/),
    ).toBeInTheDocument();
    expect(within(ligne).getByText(/clé Riot refusée \(403\)/)).toBeInTheDocument();
    expect(
      api.mock.calls.some(([req]) =>
        (req as Request).url.endsWith("/admin/riot/joueurs/1"),
      ),
    ).toBe(true);
  });

  it("lance « Tout actualiser » et affiche la progression puis le résumé avec les échecs", async () => {
    let courant = etat();
    mockApi({
      ...base,
      "GET /admin/riot": () => json(courant),
      "POST /admin/riot/tous": () => {
        courant = etat({
          enCours: true,
          nature: "Tous les joueurs",
          total: 2,
          termines: 1,
        });
        return json(courant, 202);
      },
    });
    renderApp("/administration");

    await userEvent.click(
      await screen.findByRole("button", { name: "Tout actualiser" }),
    );

    expect(await screen.findByText("1 / 2 joueurs")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Tout actualiser" }),
    ).toBeDisabled();
    expect(
      screen.getByRole("button", { name: "Actualiser Alice" }),
    ).toBeDisabled();

    courant = etat({
      nature: "Tous les joueurs",
      total: 2,
      termines: 2,
      echecs: [
        {
          playerId: 1,
          prenom: "Alice",
          message:
            "Rangs : Riot n'a pas répondu, les anciens rangs sont conservés. Cause : limite de requêtes Riot atteinte (429).",
        },
      ],
    });
    expect(
      await screen.findByText(/terminée : 2 joueurs, 1 échec/i, undefined, {
        timeout: 4000,
      }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Alice.*limite de requêtes Riot atteinte \(429\)/, {
        selector: "li",
      }),
    ).toBeInTheDocument();
  });

  it("affiche le refus quand une autre actualisation tourne déjà", async () => {
    mockApi({
      ...base,
      "GET /admin/riot": etat(),
      "POST /admin/riot/tous": problem(
        409,
        "Une actualisation Riot est déjà en cours.",
        "Conflit",
      ),
    });
    renderApp("/administration");

    await userEvent.click(
      await screen.findByRole("button", { name: "Tout actualiser" }),
    );

    await waitFor(() =>
      expect(screen.getByText(/déjà en cours/)).toBeInTheDocument(),
    );
  });
});
