import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { DraftParLien } from "@/components/draft-champions-view";
import { json, mockApi } from "@/test/api";
import { renderComponent } from "@/test/render";

const champion = (id: string, name = id): Schemas["ChampionResponse"] => ({
  id,
  key: id,
  name,
  imageUrl: `https://ddragon.test/${id}.png`,
  voies: [],
});

const champions = [champion("Ahri"), champion("Garen"), champion("Jinx"), champion("Zed")];

const tours: Schemas["TourDraftResponse"][] = Array.from({ length: 20 }, (_, i) => ({
  numero: i + 1,
  action: i < 6 || (i >= 12 && i < 16) ? "ban" : "pick",
  side: i % 2 === 0 ? "blue" : "red",
  equipe: i % 2 === 0 ? "A" : "B",
}));

const draft: Schemas["DraftChampionsResponse"] = {
  gameId: 12,
  seriesId: 5,
  fearless: true,
  equipeBlue: "A",
  equipeRed: "B",
  terminee: false,
  tourCourant: {
    numero: 2,
    action: "ban",
    side: "red",
    equipe: "B",
    commenceLe: new Date().toISOString(),
    echeance: new Date(Date.now() + 60_000).toISOString(),
  },
  tours,
  actions: [{ numero: 1, championId: "Zed" } as Schemas["ActionDraftResponse"]],
  teamA: [],
  teamB: [],
  capitaineA: null,
  capitaineB: null,
  championsDejaJoues: ["Jinx"],
  resetDemande: null,
  pause: null,
  repriseDemandee: null,
  pauseDisponibleA: true,
  pauseDisponibleB: true,
};

function mock() {
  mockApi({
    "GET /champions": champions,
    "GET /liens/lien-a": { seriesId: 5, portee: "Capitaine", equipe: "A", pret: false, adversairePret: false, pretPossible: false },
    "GET /liens/lien-a/draft-champions": draft,
    "GET /liens/lien-b": { seriesId: 5, portee: "Capitaine", equipe: "B", pret: false, adversairePret: false, pretPossible: false },
    "GET /liens/lien-b/draft-champions": draft,
    "GET /liens/lien-spec": { seriesId: 5, portee: "Spectateur", equipe: null, pret: false, adversairePret: false, pretPossible: false },
    "GET /liens/lien-spec/draft-champions": draft,
  });
}

describe("Draft de champions par lien", () => {
  it("annonce « À toi de bannir » au capitaine dont c'est le tour", async () => {
    mock();
    renderComponent(<DraftParLien lien="lien-b" portee="Capitaine" />);

    expect(await screen.findByText("À toi de bannir")).toBeInTheDocument();
    expect(screen.getByText(/Tu es le capitaine de l'Équipe B/)).toBeInTheDocument();
  });

  it("montre la grille, désactivée, au capitaine qui attend son tour, avec les champions Fearless marqués", async () => {
    mock();
    renderComponent(<DraftParLien lien="lien-a" portee="Capitaine" />);

    expect(await screen.findByText(/Au tour de l'Équipe B/)).toBeInTheDocument();
    const jinx = await screen.findByRole("button", { name: /Jinx/ });
    expect(jinx).toBeDisabled();
    expect(within(jinx).getByText("Joué")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^Bannir/ })).not.toBeInTheDocument();
  });

  it("le spectateur suit la draft en lecture seule : tour annoncé, aucune grille ni bouton", async () => {
    mock();
    renderComponent(<DraftParLien lien="lien-spec" portee="Spectateur" />);

    expect(await screen.findByText("L'Équipe B bannit")).toBeInTheDocument();
    expect(screen.getByText(/En direct · lecture seule/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Jinx/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
    expect(screen.getByRole("list", { name: "Ordre Tournament" })).toBeInTheDocument();
  });
});

describe("Prêt du Capitaine", () => {
  const attente = (acces: Partial<Schemas["AccesParLienResponse"]> = {}) => ({
    "GET /champions": champions,
    "GET /liens/lien-a": { seriesId: 5, portee: "Capitaine", equipe: "A", pret: false, adversairePret: false, pretPossible: true, ...acces },
    "GET /liens/lien-a/draft-champions": json({ status: 404 }, 404),
    "GET /liens/lien-spec": { seriesId: 5, portee: "Spectateur", equipe: null, pret: false, adversairePret: false, pretPossible: false },
    "GET /liens/lien-spec/draft-champions": json({ status: 404 }, 404),
  });

  it("propose « Je suis prêt » avant le début de la draft, et envoie le prêt", async () => {
    let envoye: unknown;
    mockApi({
      ...attente(),
      "PUT /liens/lien-a/pret": async (request: Request) => {
        envoye = await request.json();
        return json({ seriesId: 5, portee: "Capitaine", equipe: "A", pret: true, adversairePret: false, pretPossible: true });
      },
    });
    renderComponent(<DraftParLien lien="lien-a" portee="Capitaine" />);

    await userEvent.click(await screen.findByRole("button", { name: "Je suis prêt" }));

    expect(await screen.findByRole("button", { name: "Je ne suis plus prêt" })).toBeInTheDocument();
    expect(envoye).toEqual({ pret: true });
    expect(screen.getByText("L'autre Capitaine n'est pas encore prêt.")).toBeInTheDocument();
  });

  it("laisse se remettre « pas prêt » tant que l'autre Capitaine ne l'est pas", async () => {
    let envoye: unknown;
    mockApi({
      ...attente({ pret: true }),
      "PUT /liens/lien-a/pret": async (request: Request) => {
        envoye = await request.json();
        return json({ seriesId: 5, portee: "Capitaine", equipe: "A", pret: false, adversairePret: false, pretPossible: true });
      },
    });
    renderComponent(<DraftParLien lien="lien-a" portee="Capitaine" />);

    await userEvent.click(await screen.findByRole("button", { name: "Je ne suis plus prêt" }));

    expect(await screen.findByRole("button", { name: "Je suis prêt" })).toBeInTheDocument();
    expect(envoye).toEqual({ pret: false });
  });

  it("dit quand l'autre Capitaine est prêt", async () => {
    mockApi(attente({ adversairePret: true }));
    renderComponent(<DraftParLien lien="lien-a" portee="Capitaine" />);

    expect(await screen.findByText("L'autre Capitaine est prêt.")).toBeInTheDocument();
  });

  it("propose « Je suis prêt » pour la game suivante quand la draft précédente est terminée et la Série en cours", async () => {
    mockApi({
      "GET /champions": champions,
      "GET /liens/lien-a": { seriesId: 5, portee: "Capitaine", equipe: "A", pret: false, adversairePret: false, pretPossible: true },
      "GET /liens/lien-a/draft-champions": { ...draft, terminee: true, tourCourant: null },
    });
    renderComponent(<DraftParLien lien="lien-a" portee="Capitaine" />);

    expect(await screen.findByRole("button", { name: "Je suis prêt" })).toBeInTheDocument();
  });

  it("n'offre pas « Je suis prêt » pendant la draft ni quand la Série est terminée", async () => {
    mockApi({
      "GET /champions": champions,
      "GET /liens/lien-a": { seriesId: 5, portee: "Capitaine", equipe: "A", pret: false, adversairePret: false, pretPossible: false },
      "GET /liens/lien-a/draft-champions": { ...draft, terminee: true, tourCourant: null },
    });
    renderComponent(<DraftParLien lien="lien-a" portee="Capitaine" />);

    expect(await screen.findByText(/Série 5/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /prêt/ })).not.toBeInTheDocument();
  });

  it("n'offre aucun bouton au spectateur", async () => {
    mockApi(attente());
    renderComponent(<DraftParLien lien="lien-spec" portee="Spectateur" />);

    expect(await screen.findByText(/La Draft de champions n'est pas encore ouverte/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /prêt/ })).not.toBeInTheDocument();
  });
});
