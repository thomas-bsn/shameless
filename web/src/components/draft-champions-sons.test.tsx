import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { DraftParLien } from "@/components/draft-champions-view";
import { definirLecteurDeSons, type Son } from "@/lib/draft-sons";
import { json, mockApi } from "@/test/api";
import { renderComponent } from "@/test/render";

type Draft = Schemas["DraftChampionsResponse"];

const tours: Schemas["TourDraftResponse"][] = Array.from({ length: 20 }, (_, i) => ({
  numero: i + 1,
  action: i < 6 || (i >= 12 && i < 16) ? "ban" : "pick",
  side: i % 2 === 0 ? "blue" : "red",
  equipe: i % 2 === 0 ? "A" : "B",
}));

const tourDe = (numero: number): Draft["tourCourant"] => ({
  ...tours[numero - 1],
  commenceLe: new Date().toISOString(),
  echeance: new Date(Date.now() + 60_000).toISOString(),
});

const draftAu = (numero: number, terminee = false): Draft => ({
  gameId: 12,
  seriesId: 5,
  fearless: false,
  equipeBlue: "A",
  equipeRed: "B",
  terminee,
  tourCourant: terminee ? null : tourDe(numero),
  tours,
  actions: Array.from({ length: numero - 1 }, (_, i) => ({ numero: i + 1, championId: "Zed" }) as Schemas["ActionDraftResponse"]),
  teamA: [],
  teamB: [],
  capitaineA: null,
  capitaineB: null,
  championsDejaJoues: [],
  resetDemande: null,
  pause: null,
  repriseDemandee: null,
  pauseDisponibleA: true,
  pauseDisponibleB: true,
});

function lecteurSimule(disponible = true) {
  const joues: Son[] = [];
  definirLecteurDeSons({ disponible, jouer: (son) => joues.push(son) });
  return joues;
}

/** Une API dont la draft avance à la demande du test. */
function api(initial: Draft | null, equipe: "A" | "B" | null = "A") {
  const etat = { draft: initial };
  mockApi({
    "GET /champions": [],
    "GET /liens/lien": {
      seriesId: 5,
      portee: equipe ? "Capitaine" : "Spectateur",
      equipe,
      pret: false,
      adversairePret: false,
      pretPossible: false,
    },
    "GET /liens/lien/draft-champions": () => (etat.draft ? json(etat.draft) : json({ status: 404 }, 404)),
  });
  return etat;
}

const rendre = (portee: "Capitaine" | "Spectateur" = "Capitaine") =>
  renderComponent(<DraftParLien lien="lien" portee={portee} />);
const attendre = { timeout: 4000 };
const TOUR_AFFICHE = /Équipe A, c.est ton tour|Équipe A (bannit|choisit)/;

beforeEach(() => localStorage.clear());
afterEach(() => definirLecteurDeSons(null));

describe("Sons de la draft de champions", () => {
  it("joue début, ton tour, verrouillé et fin aux bons moments pour le Capitaine concerné", async () => {
    const joues = lecteurSimule();
    const etat = api(null);
    rendre();
    expect(await screen.findByText(/n'est pas encore ouverte/)).toBeInTheDocument();
    await userEvent.click(document.body);

    etat.draft = draftAu(1); // tour 1 : Équipe A
    await waitFor(() => expect(joues).toEqual(["debut", "tour"]), attendre);

    etat.draft = draftAu(2); // tour 2 : Équipe B, un champion verrouillé
    await waitFor(() => expect(joues).toEqual(["debut", "tour", "verrouille"]), attendre);

    etat.draft = draftAu(3); // tour 3 : Équipe A
    await waitFor(() => expect(joues).toEqual(["debut", "tour", "verrouille", "verrouille", "tour"]), attendre);

    etat.draft = draftAu(21, true);
    await waitFor(() => expect(joues.slice(5)).toEqual(["verrouille", "fin"]), attendre);
  }, 15000);

  it("ne joue pas « ton tour » à un spectateur, ni de son en arrivant en cours de draft", async () => {
    const joues = lecteurSimule();
    const etat = api(draftAu(3), null);
    rendre("Spectateur");
    await screen.findByText(/En direct/);
    await userEvent.click(document.body);
    expect(joues).toEqual([]);

    etat.draft = draftAu(4);
    await waitFor(() => expect(joues).toEqual(["verrouille"]), attendre);
  }, 15000);

  it("ne joue rien avant une première action de l'utilisateur sur la page", async () => {
    const joues = lecteurSimule();
    const etat = api(null);
    rendre();
    expect(await screen.findByText(/n'est pas encore ouverte/)).toBeInTheDocument();

    etat.draft = draftAu(1);
    await screen.findByText(TOUR_AFFICHE, undefined, attendre);
    expect(joues).toEqual([]);
  }, 15000);

  it("le bouton coupe le son, et le choix est retenu après rechargement", async () => {
    const joues = lecteurSimule();
    const etat = api(null);
    const { unmount } = rendre();
    await userEvent.click(await screen.findByRole("button", { name: "Couper le son" }));
    expect(screen.getByRole("button", { name: "Remettre le son" })).toBeInTheDocument();

    etat.draft = draftAu(1);
    await screen.findByText(TOUR_AFFICHE, undefined, attendre);
    expect(joues).toEqual([]);

    unmount();
    rendre();
    expect(await screen.findByRole("button", { name: "Remettre le son" })).toBeInTheDocument();
  }, 15000);

  it("sans sons configurés, la draft marche en silence et sans bouton", async () => {
    const joues = lecteurSimule(false);
    api(draftAu(1));
    rendre();
    expect(await screen.findByText(TOUR_AFFICHE)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /son/i })).not.toBeInTheDocument();
    expect(joues).toEqual([]);
  });
});
