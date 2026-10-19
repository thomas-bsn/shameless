import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { DraftParLien } from "@/components/draft-champions-view";
import { json, mockApi } from "@/test/api";
import { renderComponent } from "@/test/render";

const tours: Schemas["TourDraftResponse"][] = Array.from({ length: 20 }, (_, i) => ({
  numero: i + 1,
  action: i < 6 ? "ban" : "pick",
  side: i % 2 === 0 ? "blue" : "red",
  equipe: i % 2 === 0 ? "A" : "B",
}));

const enCours: Schemas["DraftChampionsResponse"] = {
  gameId: 12,
  seriesId: 5,
  fearless: false,
  equipeBlue: "A",
  equipeRed: "B",
  terminee: false,
  tourCourant: {
    numero: 1,
    action: "ban",
    side: "blue",
    equipe: "A",
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
  pause: null,
  repriseDemandee: null,
  pauseDemandee: null,
  pauseDisponibleA: true,
  pauseDisponibleB: true,
  resetDemande: null,
};

const enPause = (extra: Partial<Schemas["DraftChampionsResponse"]> = {}): Schemas["DraftChampionsResponse"] => ({
  ...enCours,
  pause: {
    parEquipe: "A",
    commenceLe: new Date().toISOString(),
    finLe: new Date(Date.now() + 5 * 60_000).toISOString(),
    tempsRestantTourMs: 40_000,
  },
  pauseDisponibleA: false,
  ...extra,
});

const demandePause = (parEquipe: string, dansMs = 60_000): Schemas["PauseDemandeeResponse"] => ({
  parEquipe,
  demandeLe: new Date().toISOString(),
  expireLe: new Date(Date.now() + dansMs).toISOString(),
});

const demandeReprise = (parEquipe: string): Schemas["RepriseDemandeeResponse"] => ({
  parEquipe,
  expireLe: new Date(Date.now() + 60_000).toISOString(),
});

const acces = (portee: "Capitaine" | "Spectateur", equipe: string | null) => ({
  seriesId: 5,
  portee,
  equipe,
  pret: true,
  adversairePret: true,
});

describe("Pause de la draft de champions", () => {
  it("le spectateur voit « Draft en pause » avec le décompte de la reprise automatique", async () => {
    mockApi({
      "GET /champions": [],
      "GET /liens/lien-spec": acces("Spectateur", null),
      "GET /liens/lien-spec/draft-champions": enPause(),
    });
    renderComponent(<DraftParLien lien="lien-spec" portee="Spectateur" />);

    expect(await screen.findByText("Draft en pause")).toBeInTheDocument();
    expect(screen.getByLabelText("Reprise automatique dans")).toHaveTextContent(/^[45]:\d\d$/);
    expect(screen.getByLabelText("Temps restant du tour")).toHaveTextContent("40 s");
    expect(screen.queryByRole("button", { name: /pause/i })).not.toBeInTheDocument();
  });

  it("le capitaine demande la pause : seule une demande part, il attend l'autre Capitaine", async () => {
    let etat = enCours;
    let appels = 0;
    mockApi({
      "GET /champions": [],
      "GET /liens/lien-a": acces("Capitaine", "A"),
      "GET /liens/lien-a/draft-champions": () => json(etat),
      "POST /liens/lien-a/draft-champions/pause": () => {
        appels++;
        etat = { ...enCours, pauseDemandee: demandePause("A") };
        return json(etat);
      },
    });
    renderComponent(<DraftParLien lien="lien-a" portee="Capitaine" />);

    await userEvent.click(await screen.findByRole("button", { name: "Mettre en pause" }));

    const attente = await screen.findByTestId("avis-de-vote");
    expect(attente).toHaveTextContent("Pause demandée");
    expect(attente).toHaveTextContent(/En attente de la réponse de l'autre Capitaine/);
    expect(appels).toBe(1);
    expect(screen.queryByText("Draft en pause")).not.toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Mettre en pause" })).not.toBeInTheDocument();
  });

  it("l'autre capitaine répond dans une fenêtre modale au centre et accepte : la draft passe en pause", async () => {
    let etat: Schemas["DraftChampionsResponse"] = { ...enCours, pauseDemandee: demandePause("A") };
    let appels = 0;
    mockApi({
      "GET /champions": [],
      "GET /liens/lien-b": acces("Capitaine", "B"),
      "GET /liens/lien-b/draft-champions": () => json(etat),
      "POST /liens/lien-b/draft-champions/pause/acceptation": () => {
        appels++;
        etat = enPause();
        return json(etat);
      },
    });
    renderComponent(<DraftParLien lien="lien-b" portee="Capitaine" />);

    const modale = await screen.findByRole("dialog", { name: "Pause demandée" });
    expect(modale).toHaveTextContent("L'Équipe A demande une pause de la draft");
    expect(within(modale).getByLabelText("Temps pour répondre")).toHaveTextContent(/^(59|60) s$/);
    await userEvent.click(within(modale).getByRole("button", { name: "Accepter" }));

    expect(await screen.findByText("Draft en pause")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(appels).toBe(1);
  });

  it("l'autre capitaine refuse la pause : la fenêtre se ferme et la draft continue", async () => {
    let etat: Schemas["DraftChampionsResponse"] = { ...enCours, pauseDemandee: demandePause("A") };
    let appels = 0;
    mockApi({
      "GET /champions": [],
      "GET /liens/lien-b": acces("Capitaine", "B"),
      "GET /liens/lien-b/draft-champions": () => json(etat),
      "POST /liens/lien-b/draft-champions/pause/refus": () => {
        appels++;
        etat = enCours;
        return json(etat);
      },
    });
    renderComponent(<DraftParLien lien="lien-b" portee="Capitaine" />);

    const modale = await screen.findByRole("dialog", { name: "Pause demandée" });
    await userEvent.click(within(modale).getByRole("button", { name: "Refuser" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(appels).toBe(1);
    expect(screen.queryByText("Draft en pause")).not.toBeInTheDocument();
  });

  it("la fenêtre se ferme toute seule quand la demande de pause expire", async () => {
    mockApi({
      "GET /champions": [],
      "GET /liens/lien-b": acces("Capitaine", "B"),
      "GET /liens/lien-b/draft-champions": {
        ...enCours,
        pauseDemandee: demandePause("A", 1200),
      },
    });
    renderComponent(<DraftParLien lien="lien-b" portee="Capitaine" />);

    await screen.findByRole("dialog", { name: "Pause demandée" });
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument(), { timeout: 3000 });
  });

  it("le spectateur voit « Pause demandée » dans un avis centré, sans fenêtre modale ni bouton", async () => {
    mockApi({
      "GET /champions": [],
      "GET /liens/lien-spec": acces("Spectateur", null),
      "GET /liens/lien-spec/draft-champions": { ...enCours, pauseDemandee: demandePause("A") },
    });
    renderComponent(<DraftParLien lien="lien-spec" portee="Spectateur" />);

    const avis = await screen.findByTestId("avis-de-vote");
    expect(avis).toHaveTextContent("Pause demandée");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(within(avis).queryByRole("button")).not.toBeInTheDocument();
  });

  it("le spectateur voit « Draft en pause » quand la pause démarre", async () => {
    let etat: Schemas["DraftChampionsResponse"] = { ...enCours, pauseDemandee: demandePause("A") };
    mockApi({
      "GET /champions": [],
      "GET /liens/lien-spec": acces("Spectateur", null),
      "GET /liens/lien-spec/draft-champions": () => json(etat),
    });
    renderComponent(<DraftParLien lien="lien-spec" portee="Spectateur" />);
    await screen.findByTestId("avis-de-vote");

    etat = enPause();

    await waitFor(() => expect(screen.getByTestId("avis-de-vote")).toHaveTextContent("Draft en pause"), {
      timeout: 4000,
    });
  });

  it("n'offre plus la pause au capitaine qui l'a utilisée", async () => {
    mockApi({
      "GET /champions": [],
      "GET /liens/lien-a": acces("Capitaine", "A"),
      "GET /liens/lien-a/draft-champions": {
        ...enCours,
        pauseDisponibleA: false,
      },
    });
    renderComponent(<DraftParLien lien="lien-a" portee="Capitaine" />);

    await screen.findByText(/Tu es le capitaine/);
    expect(screen.queryByRole("button", { name: "Mettre en pause" })).not.toBeInTheDocument();
  });

  it("le capitaine demande la reprise, l'autre répond dans une fenêtre modale et accepte", async () => {
    let etat: Schemas["DraftChampionsResponse"] = enPause();
    mockApi({
      "GET /champions": [],
      "GET /liens/lien-a": acces("Capitaine", "A"),
      "GET /liens/lien-a/draft-champions": () => json(etat),
      "POST /liens/lien-a/draft-champions/reprise": () => {
        etat = enPause({ repriseDemandee: demandeReprise("A") });
        return json(etat);
      },
      "GET /liens/lien-b": acces("Capitaine", "B"),
      "GET /liens/lien-b/draft-champions": () => json(etat),
      "POST /liens/lien-b/draft-champions/reprise/acceptation": () => {
        etat = enCours;
        return json(etat);
      },
    });
    const { unmount } = renderComponent(<DraftParLien lien="lien-a" portee="Capitaine" />);
    await userEvent.click(await screen.findByRole("button", { name: "Demander la reprise" }));
    expect(await screen.findByTestId("avis-de-vote")).toHaveTextContent(/En attente de la réponse de l'autre Capitaine/);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    unmount();

    renderComponent(<DraftParLien lien="lien-b" portee="Capitaine" />);
    const modale = await screen.findByRole("dialog", { name: "Reprise demandée" });
    expect(modale).toHaveTextContent(/L'Équipe A demande la reprise/);
    expect(within(modale).getByRole("button", { name: "Refuser" })).toBeInTheDocument();
    await userEvent.click(within(modale).getByRole("button", { name: "Accepter" }));

    expect(await screen.findByText(/Au tour de l'Équipe A/)).toBeInTheDocument();
    expect(screen.queryByText("Draft en pause")).not.toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("le spectateur voit « Reprise demandée » sans fenêtre modale", async () => {
    mockApi({
      "GET /champions": [],
      "GET /liens/lien-spec": acces("Spectateur", null),
      "GET /liens/lien-spec/draft-champions": enPause({ repriseDemandee: demandeReprise("A") }),
    });
    renderComponent(<DraftParLien lien="lien-spec" portee="Spectateur" />);

    expect(await screen.findByTestId("avis-de-vote")).toHaveTextContent("Reprise demandée");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});
