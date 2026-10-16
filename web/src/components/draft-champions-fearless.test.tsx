import { screen, within } from "@testing-library/react";
import type { Schemas } from "@/api/client";
import { DraftChampionsOrganisateur } from "@/components/draft-champions-organisateur";
import { DraftParLien } from "@/components/draft-champions-view";
import { mockApi } from "@/test/api";
import { renderComponent } from "@/test/render";

const champion = (id: string, name = id): Schemas["ChampionResponse"] => ({
  id,
  key: id,
  name,
  imageUrl: `https://ddragon.test/${id}.png`,
});

const champions = [champion("Ahri"), champion("Garen"), champion("LeeSin", "Lee Sin"), champion("Zed")];

const tours: Schemas["TourDraftResponse"][] = Array.from({ length: 20 }, (_, i) => ({
  numero: i + 1,
  action: i < 6 || (i >= 12 && i < 16) ? "ban" : "pick",
  side: i % 2 === 0 ? "blue" : "red",
  equipe: i % 2 === 0 ? "A" : "B",
}));

const draft = (championsDejaJoues: string[] | null): Schemas["DraftChampionsResponse"] => ({
  gameId: 12,
  seriesId: 5,
  fearless: championsDejaJoues !== null,
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
  championsDejaJoues,
});

const series = { seriesId: 5, capitaineA: 1, capitaineB: 6 } as Schemas["SeriesResponse"];

function mockDraft(body: Schemas["DraftChampionsResponse"]) {
  mockApi({
    "GET /champions": champions,
    "GET /liens/lien-a": { seriesId: 5, portee: "Capitaine", equipe: "A" },
    "GET /liens/lien-a/draft-champions": body,
    "GET /liens/lien-spec": { seriesId: 5, portee: "Spectateur", equipe: null },
    "GET /liens/lien-spec/draft-champions": body,
    "GET /games/12/draft-champions": body,
  });
}

const vues = [
  ["capitaine", () => <DraftParLien lien="lien-a" portee="Capitaine" />],
  ["spectateur", () => <DraftParLien lien="lien-spec" portee="Spectateur" />],
  ["organisateur", () => <DraftChampionsOrganisateur series={series} gameId={12} />],
] as const;

describe("Fearless dans la Draft de champions", () => {
  it.each(vues)("la vue %s liste les champions déjà joués dans la Série", async (_, vue) => {
    mockDraft(draft(["Garen", "LeeSin"]));

    renderComponent(vue());

    const liste = await screen.findByRole("list", { name: "Champions déjà joués (Fearless)" });
    expect(await within(liste).findByText("Garen")).toBeInTheDocument();
    expect(within(liste).getByText("Lee Sin")).toBeInTheDocument();
    expect(within(liste).queryByText("Ahri")).not.toBeInTheDocument();
  });

  it.each(vues)("sans Fearless, la vue %s n'affiche aucune liste", async (_, vue) => {
    mockDraft(draft(null));

    renderComponent(vue());

    expect(await screen.findByLabelText("Tour courant")).toBeInTheDocument();
    expect(screen.queryByRole("list", { name: "Champions déjà joués (Fearless)" })).not.toBeInTheDocument();
  });

  it("le capitaine voit les champions déjà joués signalés mais toujours sélectionnables", async () => {
    mockDraft(draft(["Garen"]));

    renderComponent(<DraftParLien lien="lien-a" portee="Capitaine" />);

    const garen = await screen.findByRole("button", { name: /Garen.*déjà joué/ });
    expect(garen).toBeEnabled();
    expect(screen.getByRole("button", { name: "Ahri" })).toBeEnabled();
  });
});
