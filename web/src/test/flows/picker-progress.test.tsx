import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const participant = (playerId: number, prenom: string, cost: number): Schemas["GameParticipantDto"] => ({
  playerId,
  prenom,
  riotId: `${prenom}#EUW`,
  rankTier: null,
  rankDivision: null,
  cost,
  role: null,
});

const picker = (overrides: Partial<Schemas["PickerResponse"]> = {}): Schemas["PickerResponse"] => ({
  players: Array.from({ length: 10 }, (_, index) => participant(index + 1, `P${index + 1}`, 2)),
  methode: null,
  variante: null,
  teamA: [],
  teamB: [],
  teamACost: 0,
  teamBCost: 0,
  ecartCost: 0,
  canValidate: false,
  capitaineA: null,
  capitaineB: null,
  premierPick: null,
  picks: [],
  equipeAuTrait: null,
  ...overrides,
});

const labels = ["Joueurs", "Méthode", "Draft", "Équipes", "Autorole", "Série"];
const soiree = (activePicker: Schemas["PickerResponse"] | null): Schemas["OpenSoireeResponse"] => ({
  pickerInterrompu: false,
  soiree: { id: 7, status: "Open", openedAt: "2026-10-16T19:00:00Z", closedAt: null, picker: activePicker },
});

beforeEach(() => {
  vi.stubGlobal("ResizeObserver", class { observe() {} unobserve() {} disconnect() {} });
});

describe("Progression du Picker", () => {
  it("affiche les six étapes et marque la sélection courante", async () => {
    mockApi({ "GET /auth/me": user(), "GET /soirees/open": soiree(null), "GET /players": [] });
    renderApp("/soiree/picker");

    const progress = await screen.findByRole("list", { name: "Étapes du Picker" });
    expect(within(progress).getAllByRole("listitem").map((item) => item.textContent)).toEqual(labels);
    expect(within(progress).getByText("Joueurs")).toHaveAttribute("aria-current", "step");
  });

  it("marque l'étape Draft quand les capitaines font leurs picks", async () => {
    const players = Array.from({ length: 10 }, (_, index) => participant(index + 1, `P${index + 1}`, 2));
    mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": soiree(picker({ players, methode: "Draft", equipeAuTrait: "A" })),
    });
    renderApp("/soiree/picker");

    const progress = await screen.findByRole("list", { name: "Étapes du Picker" });
    expect(within(progress).getAllByRole("listitem").map((item) => item.textContent)).toEqual(labels);
    expect(within(progress).getByText("Draft")).toHaveAttribute("aria-current", "step");
  });

  it("ouvre la fenêtre du pile ou face et affiche son résultat avant de reprendre la Draft", async () => {
    const players = Array.from({ length: 10 }, (_, index) => participant(index + 1, `P${index + 1}`, 2));
    const result = picker({ players, methode: "Draft", capitaineA: 1, capitaineB: 2, premierPick: "B", equipeAuTrait: "B" });
    const api = mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": soiree(picker({ players })),
      "POST /soirees/open/picker/draft": result,
    });
    renderApp("/soiree/picker");

    await userEvent.click(await screen.findByRole("button", { name: "Choisir la Draft" }));
    const dialog = await screen.findByRole("dialog", { name: "Configurer la Draft" });
    await userEvent.click(within(dialog).getByRole("button", { name: "Lancer le pile ou face" }));

    expect(await screen.findByLabelText("Résultat du pile ou face")).toHaveTextContent("Équipe B choisit en premier");
    expect(api.mock.calls.some(([request]) => request.method === "POST" && request.url.endsWith("/soirees/open/picker/draft"))).toBe(true);
    await userEvent.click(within(dialog).getByRole("button", { name: "Continuer la Draft" }));
  });

  it("affiche les coûts du tirage dans la fenêtre de Roulette", async () => {
    const players = Array.from({ length: 10 }, (_, index) => participant(index + 1, `P${index + 1}`, index < 5 ? 3 : 4));
    const result = picker({
      players,
      methode: "Roulette",
      variante: "Equilibree",
      teamA: players.slice(0, 5),
      teamB: players.slice(5),
      teamACost: 15,
      teamBCost: 20,
      ecartCost: 5,
      canValidate: true,
    });
    mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": soiree(picker({ players })),
      "POST /soirees/open/picker/roulette": result,
    });
    renderApp("/soiree/picker");

    await userEvent.click(await screen.findByRole("button", { name: "Choisir la Roulette" }));
    const dialog = await screen.findByRole("dialog", { name: "Tirage de la Roulette" });
    await userEvent.click(within(dialog).getByRole("button", { name: "Roulette Équilibrée" }));

    expect(await within(dialog).findByText(/Cost A : 15 pts · Cost B : 20 pts · Écart : 5 pts/)).toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: "Continuer avec ces Équipes" })).toBeInTheDocument();
  });

  it("sépare les étapes Équipes, Autorole et Série après la Roulette", async () => {
    const players = Array.from({ length: 10 }, (_, index) => participant(index + 1, `P${index + 1}`, 3));
    const initial = picker({ players });
    const result = picker({
      players,
      methode: "Roulette",
      variante: "Equilibree",
      teamA: players.slice(0, 5),
      teamB: players.slice(5),
      teamACost: 15,
      teamBCost: 15,
      canValidate: true,
    });
    let hasTeams = false;
    mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": () => new Response(JSON.stringify(soiree(hasTeams ? result : initial)), { headers: { "Content-Type": "application/json" } }),
      "POST /soirees/open/picker/roulette": () => {
        hasTeams = true;
        return new Response(JSON.stringify(result), { headers: { "Content-Type": "application/json" } });
      },
    });
    renderApp("/soiree/picker");

    await userEvent.click(await screen.findByRole("button", { name: "Choisir la Roulette" }));
    const dialog = await screen.findByRole("dialog", { name: "Tirage de la Roulette" });
    await userEvent.click(within(dialog).getByRole("button", { name: "Roulette Équilibrée" }));
    await userEvent.click(await within(dialog).findByRole("button", { name: "Continuer avec ces Équipes" }));

    expect(await screen.findByRole("button", { name: "Continuer vers l'Autorole" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Continuer vers l'Autorole" }));
    const progress = screen.getByRole("list", { name: "Étapes du Picker" });
    expect(within(progress).getByText("Autorole")).toHaveAttribute("aria-current", "step");
    await userEvent.click(screen.getByRole("button", { name: "Passer l'Autorole" }));
    expect(within(progress).getByText("Série")).toHaveAttribute("aria-current", "step");
    expect(screen.getByText("Créer la Série")).toBeInTheDocument();
  });

  it("ouvre une fenêtre de confirmation avec le cost avant et après le pick", async () => {
    const players = Array.from({ length: 10 }, (_, index) => participant(index + 1, `P${index + 1}`, index === 2 ? 4 : 2));
    const active = picker({
      players,
      methode: "Draft",
      teamA: [players[0]],
      teamB: [players[1]],
      teamACost: 2,
      teamBCost: 2,
      capitaineA: 1,
      capitaineB: 2,
      premierPick: "A",
      equipeAuTrait: "A",
    });
    const api = mockApi({ "GET /auth/me": user(), "GET /soirees/open": soiree(active) });
    renderApp("/soiree/picker");

    await userEvent.click(await screen.findByRole("button", { name: "Prendre P3 dans l'Équipe A" }));

    const dialog = await screen.findByRole("dialog", { name: "Confirmer le choix de P3" });
    expect(dialog).toHaveTextContent("Cost de l'Équipe A avant : 2");
    expect(dialog).toHaveTextContent("Cost de l'Équipe A après : 6");
    expect(api.mock.calls.some(([request]) => request.url.endsWith("/soirees/open/picker/draft/picks"))).toBe(false);

    await userEvent.click(within(dialog).getByRole("button", { name: "Confirmer le choix" }));
    expect(api.mock.calls.some(([request]) => request.url.endsWith("/soirees/open/picker/draft/picks"))).toBe(true);
  });
});
