import { screen } from "@testing-library/react";
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

const acces = (portee: "Capitaine" | "Spectateur", equipe: string | null) => ({
  seriesId: 5,
  portee,
  equipe,
  pret: true,
  adversairePret: true,
  pretPossible: false,
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

  it("le capitaine met la draft en pause", async () => {
    let etat = enCours;
    let appels = 0;
    mockApi({
      "GET /champions": [],
      "GET /liens/lien-a": acces("Capitaine", "A"),
      "GET /liens/lien-a/draft-champions": () => json(etat),
      "POST /liens/lien-a/draft-champions/pause": () => {
        appels++;
        etat = enPause();
        return json(etat);
      },
    });
    renderComponent(<DraftParLien lien="lien-a" portee="Capitaine" />);

    await userEvent.click(await screen.findByRole("button", { name: "Mettre en pause" }));

    expect(await screen.findByText("Draft en pause")).toBeInTheDocument();
    expect(appels).toBe(1);
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

  it("le capitaine demande la reprise, l'autre l'accepte", async () => {
    const demande = () => ({ parEquipe: "A", expireLe: new Date(Date.now() + 60_000).toISOString() });
    let etat = enPause();
    mockApi({
      "GET /champions": [],
      "GET /liens/lien-a": acces("Capitaine", "A"),
      "GET /liens/lien-a/draft-champions": () => json(etat),
      "POST /liens/lien-a/draft-champions/reprise": () => {
        etat = enPause({ repriseDemandee: demande() });
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
    expect(await screen.findByText(/En attente de l'autre Capitaine/)).toBeInTheDocument();
    unmount();

    renderComponent(<DraftParLien lien="lien-b" portee="Capitaine" />);
    expect(await screen.findByText(/L'autre Capitaine demande la reprise/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Refuser" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Accepter" }));

    expect(await screen.findByText(/Au tour de l'Équipe A/)).toBeInTheDocument();
    expect(screen.queryByText("Draft en pause")).not.toBeInTheDocument();
  });
});
