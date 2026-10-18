import { screen, within } from "@testing-library/react";
import type { Schemas } from "@/api/client";
import { DraftParLien } from "@/components/draft-champions-view";
import { mockApi } from "@/test/api";
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
};

function mock() {
  mockApi({
    "GET /champions": champions,
    "GET /liens/lien-a": { seriesId: 5, portee: "Capitaine", equipe: "A" },
    "GET /liens/lien-a/draft-champions": draft,
    "GET /liens/lien-b": { seriesId: 5, portee: "Capitaine", equipe: "B" },
    "GET /liens/lien-b/draft-champions": draft,
    "GET /liens/lien-spec": { seriesId: 5, portee: "Spectateur", equipe: null },
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
