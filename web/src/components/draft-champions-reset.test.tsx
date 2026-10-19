import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { DraftParLien } from "@/components/draft-champions-view";
import { json, mockApi, problem } from "@/test/api";
import { renderComponent } from "@/test/render";

const champions: Schemas["ChampionResponse"][] = ["Ahri", "Zed"].map((id) => ({
  id,
  key: id,
  name: id,
  imageUrl: `https://ddragon.test/${id}.png`,
  voies: [],
}));

const tours: Schemas["TourDraftResponse"][] = Array.from(
  { length: 20 },
  (_, i) => ({
    numero: i + 1,
    action: i < 6 || (i >= 12 && i < 16) ? "ban" : "pick",
    side: i % 2 === 0 ? "blue" : "red",
    equipe: i % 2 === 0 ? "A" : "B",
  }),
);

const draft: Schemas["DraftChampionsResponse"] = {
  gameId: 12,
  seriesId: 5,
  fearless: false,
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
  actions: [],
  teamA: [],
  teamB: [],
  capitaineA: null,
  capitaineB: null,
  championsDejaJoues: null,
  resetDemande: null,
  pause: null,
  repriseDemandee: null,
  pauseDisponibleA: true,
  pauseDisponibleB: true,
};

const avecDemande: Schemas["DraftChampionsResponse"] = {
  ...draft,
  resetDemande: {
    equipe: "A",
    demandeLe: new Date().toISOString(),
    expireLe: new Date(Date.now() + 60_000).toISOString(),
  },
};

const sansContenu = () => new Response(null, { status: 204 });

const base = (d: Schemas["DraftChampionsResponse"]) => ({
  "GET /champions": champions,
  "GET /liens/lien-a": {
    seriesId: 5,
    portee: "Capitaine",
    equipe: "A",
    pret: true,
    adversairePret: true,
    pretPossible: false,
  },
  "GET /liens/lien-a/draft-champions": d,
  "GET /liens/lien-b": {
    seriesId: 5,
    portee: "Capitaine",
    equipe: "B",
    pret: true,
    adversairePret: true,
    pretPossible: false,
  },
  "GET /liens/lien-b/draft-champions": d,
  "GET /liens/lien-spec": {
    seriesId: 5,
    portee: "Spectateur",
    equipe: null,
    pret: false,
    adversairePret: false,
    pretPossible: false,
  },
  "GET /liens/lien-spec/draft-champions": d,
});

describe("Reset de la draft", () => {
  it("un Capitaine demande un Reset", async () => {
    let demandes = 0;
    mockApi({
      ...base(draft),
      "POST /liens/lien-a/draft-champions/reset": () => {
        demandes++;
        return sansContenu();
      },
    });
    renderComponent(<DraftParLien lien="lien-a" portee="Capitaine" />);

    await userEvent.click(
      await screen.findByRole("button", {
        name: "Demander un Reset de la draft",
      }),
    );

    expect(demandes).toBe(1);
  });

  it("le demandeur voit sa demande en attente, sans bouton de réponse", async () => {
    mockApi(base(avecDemande));
    renderComponent(<DraftParLien lien="lien-a" portee="Capitaine" />);

    expect(await screen.findByText(/Reset demandé/)).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Accepter le Reset" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Demander un Reset de la draft" }),
    ).not.toBeInTheDocument();
  });

  it("l'autre Capitaine accepte la demande", async () => {
    let envoye: unknown;
    mockApi({
      ...base(avecDemande),
      "POST /liens/lien-b/draft-champions/reset/reponse": async (
        request: Request,
      ) => {
        envoye = await request.json();
        return sansContenu();
      },
    });
    renderComponent(<DraftParLien lien="lien-b" portee="Capitaine" />);

    expect(
      await screen.findByText(/L'Équipe A demande à remettre la draft à zéro/),
    ).toBeInTheDocument();
    await userEvent.click(
      screen.getByRole("button", { name: "Accepter le Reset" }),
    );

    expect(envoye).toEqual({ accepte: true });
  });

  it("l'autre Capitaine refuse la demande", async () => {
    let envoye: unknown;
    mockApi({
      ...base(avecDemande),
      "POST /liens/lien-b/draft-champions/reset/reponse": async (
        request: Request,
      ) => {
        envoye = await request.json();
        return sansContenu();
      },
    });
    renderComponent(<DraftParLien lien="lien-b" portee="Capitaine" />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Refuser le Reset" }),
    );

    expect(envoye).toEqual({ accepte: false });
  });

  it("le spectateur voit « Reset demandé » puis « Draft remise à zéro », sans bouton", async () => {
    let remise = false;
    mockApi({
      ...base(avecDemande),
      "GET /liens/lien-spec/draft-champions": () =>
        remise ? problem(404) : json(avecDemande),
    });
    renderComponent(<DraftParLien lien="lien-spec" portee="Spectateur" />);

    expect(await screen.findByText(/Reset demandé/)).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /Reset/ }),
    ).not.toBeInTheDocument();

    remise = true;
    expect(
      await screen.findByText("Draft remise à zéro", {}, { timeout: 4000 }),
    ).toBeInTheDocument();
  });

  it("ne propose aucun Reset une fois la draft terminée", async () => {
    mockApi(base({ ...draft, terminee: true, tourCourant: null }));
    renderComponent(<DraftParLien lien="lien-a" portee="Capitaine" />);

    expect(await screen.findByText("Draft terminée")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Demander un Reset de la draft" }),
    ).not.toBeInTheDocument();
  });
});
