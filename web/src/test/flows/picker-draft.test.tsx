import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { json, mockApi } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Picker = Schemas["PickerResponse"];
type Participant = Schemas["GameParticipantDto"];

const participant = (playerId: number, cost: number): Participant => ({
  playerId,
  prenom: `Joueur${playerId}`,
  riotId: `J${playerId}#EUW`,
  rankTier: null,
  rankDivision: null,
  cost,
  role: null,
});

const dix = Array.from({ length: 10 }, (_, i) => participant(i + 1, 3));

const picker = (overrides: Partial<Picker> = {}): Picker => ({
  players: dix,
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

const joueurs = Array.from({ length: 10 }, (_, i) =>
  player({ id: i + 1, prenom: `Joueur${i + 1}`, riotId: `J${i + 1}#EUW` }),
);

const somme = (equipe: Participant[]) => equipe.reduce((s, p) => s + (p.cost ?? 0), 0);

/** La Draft après le désignation des capitaines (Joueur1 cost 5 en A, Joueur2 cost 4 en B), A au trait. */
const apresCapitaines = (): Picker =>
  picker({
    methode: "Draft",
    teamA: [{ ...dix[0]!, cost: 5 }],
    teamB: [{ ...dix[1]!, cost: 4 }],
    teamACost: 5,
    teamBCost: 4,
    capitaineA: 1,
    capitaineB: 2,
    premierPick: "A",
    equipeAuTrait: "A",
  });

const draftTerminee = (): Picker => {
  const teamA = [{ ...dix[0]!, cost: 5 }, ...dix.slice(2, 6)];
  const teamB = [{ ...dix[1]!, cost: 4 }, ...dix.slice(6)];
  return picker({
    methode: "Draft",
    teamA,
    teamB,
    teamACost: somme(teamA),
    teamBCost: somme(teamB),
    capitaineA: 1,
    capitaineB: 2,
    premierPick: "A",
    canValidate: true,
  });
};

// jsdom n'a pas ResizeObserver, dont les cases à cocher de Radix ont besoin dans un formulaire.
beforeEach(() => {
  vi.stubGlobal(
    "ResizeObserver",
    class {
      observe() {}
      unobserve() {}
      disconnect() {}
    },
  );
});

function mockDraft(apresPick: () => Picker = apresCapitaines) {
  let courant: Picker = picker();
  let lancement: unknown = null;
  let pick: unknown = null;
  mockApi({
    "GET /auth/me": user(),
    "GET /soirees/open": () =>
      json({ soiree: { id: 7, status: "Open", openedAt: "2026-10-16T19:00:00Z", closedAt: null, picker: courant } }),
    "GET /players": joueurs,
    "POST /soirees/open/picker/draft": async (req) => {
      lancement = await req.json();
      courant = apresCapitaines();
      return json(courant);
    },
    "POST /soirees/open/picker/draft/picks": async (req) => {
      pick = await req.json();
      courant = apresPick();
      return json(courant);
    },
  });
  return { lancement: () => lancement, pick: () => pick };
}

async function jusquAuChoixDuCapitaine() {
  await userEvent.click(await screen.findByRole("button", { name: "Lancer la Draft" }));
  const pile = await screen.findByRole("dialog", { name: "Pile ou face" });
  await userEvent.click(within(pile).getByRole("button", { name: "Lancer la pièce" }));
  await userEvent.click(within(pile).getByRole("button", { name: "Commencer la Draft" }));
  return screen.findByRole("dialog", { name: "Choix du capitaine" });
}

describe("Picker : Draft avec ses fenêtres", () => {
  it("désigne le premier à choisir dans la fenêtre de pile ou face", async () => {
    const { lancement } = mockDraft();
    renderApp("/soiree/picker");

    await userEvent.click(await screen.findByRole("button", { name: "Lancer la Draft" }));
    const fenetre = await screen.findByRole("dialog", { name: "Pile ou face" });

    expect(lancement()).toMatchObject({ capitaines: "PlusGrosCosts", premierPick: null });
    expect(within(fenetre).getByText("Qui choisit en premier ?")).toBeInTheDocument();
    expect(within(fenetre).getByText(/Joueur1, Équipe A/)).toBeInTheDocument();
    expect(within(fenetre).getByText(/Joueur2, Équipe B/)).toBeInTheDocument();

    await userEvent.click(within(fenetre).getByRole("button", { name: "Lancer la pièce" }));
    expect(within(fenetre).getByText("Joueur1 choisit en premier")).toBeInTheDocument();
  });

  it("montre le cost de l'équipe avant et après le joueur sélectionné", async () => {
    mockDraft();
    renderApp("/soiree/picker");

    const fenetre = await jusquAuChoixDuCapitaine();

    expect(within(fenetre).getByText("Joueur1, à toi de choisir")).toBeInTheDocument();
    expect(within(fenetre).getByText("Équipe A · choix 3 sur 10")).toBeInTheDocument();
    expect(within(fenetre).getByRole("button", { name: "Choisir" })).toBeDisabled();

    await userEvent.click(within(fenetre).getByRole("radio", { name: /Joueur5/ }));

    expect(within(fenetre).getByText("Équipe A : 5 pts → 8 pts avec Joueur5")).toBeInTheDocument();
    expect(within(fenetre).getByText("Équipe B : 4 pts")).toBeInTheDocument();
  });

  it("place le joueur choisi puis rejoint l'étape Équipes à dix joueurs", async () => {
    const { pick } = mockDraft(draftTerminee);
    renderApp("/soiree/picker");
    const fenetre = await jusquAuChoixDuCapitaine();

    await userEvent.click(within(fenetre).getByRole("radio", { name: /Joueur5/ }));
    await userEvent.click(within(fenetre).getByRole("button", { name: "Choisir Joueur5" }));

    await vi.waitFor(() => expect(pick()).toEqual({ equipe: "A", playerId: 5 }));
    expect(await screen.findByText("Voilà les équipes")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    const etapes = within(screen.getByRole("list", { name: "Étapes du Picker" }));
    expect(etapes.getByText("Équipes").closest("li")).toHaveAttribute("aria-current", "step");
  });

  it("laisse rouvrir la fenêtre du capitaine après Annuler", async () => {
    mockDraft();
    renderApp("/soiree/picker");
    const fenetre = await jusquAuChoixDuCapitaine();

    await userEvent.click(within(fenetre).getByRole("button", { name: "Annuler" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Joueur1, choisis un joueur" }));

    expect(await screen.findByRole("dialog", { name: "Choix du capitaine" })).toBeInTheDocument();
  });
});

describe("Picker : capitaines distincts", () => {
  // jsdom n'implémente pas ces API, que le menu déroulant de Radix appelle.
  beforeEach(() => {
    Element.prototype.hasPointerCapture ??= () => false;
    Element.prototype.setPointerCapture ??= () => {};
    Element.prototype.releasePointerCapture ??= () => {};
    Element.prototype.scrollIntoView ??= () => {};
  });

  async function ouvrirLaListe(nom: string) {
    await userEvent.click(await screen.findByRole("combobox", { name: nom }));
    return screen.findAllByRole("option");
  }

  it("grise en B le joueur choisi capitaine en A, et inversement", async () => {
    mockDraft();
    renderApp("/soiree/picker");
    await userEvent.click(await screen.findByRole("combobox", { name: "Capitaines" }));
    await userEvent.click(await screen.findByRole("option", { name: "À la main" }));

    await ouvrirLaListe("Capitaine A");
    await userEvent.click(screen.getByRole("option", { name: "Joueur3" }));

    await ouvrirLaListe("Capitaine B");
    expect(screen.getByRole("option", { name: "Joueur3" })).toHaveAttribute("aria-disabled", "true");
    expect(screen.getByRole("option", { name: "Joueur4" })).not.toHaveAttribute("aria-disabled", "true");
    await userEvent.click(screen.getByRole("option", { name: "Joueur4" }));

    await ouvrirLaListe("Capitaine A");
    expect(screen.getByRole("option", { name: "Joueur4" })).toHaveAttribute("aria-disabled", "true");
    expect(screen.getByRole("option", { name: "Joueur3" })).not.toHaveAttribute("aria-disabled", "true");
  });
});

describe("Picker : récap sans couronne de capitaine", () => {
  it("ne marque aucun capitaine dans les cartes d'Équipe", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /soirees/open": {
        soiree: { id: 7, status: "Open", openedAt: "2026-10-16T19:00:00Z", closedAt: null, picker: draftTerminee() },
      },
      "GET /players": joueurs,
    });
    renderApp("/soiree/picker");

    expect(await screen.findByRole("group", { name: "Équipe A" })).toBeInTheDocument();
    expect(screen.queryByLabelText("Capitaine")).not.toBeInTheDocument();
  });
});
