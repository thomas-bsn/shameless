import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { DraftParLien } from "@/components/draft-champions-view";
import { mockApi } from "@/test/api";
import { renderComponent } from "@/test/render";

const champion = (id: string, name = id, voies: string[] = []): Schemas["ChampionResponse"] => ({
  id,
  key: id,
  name,
  imageUrl: `https://ddragon.test/${id}.png`,
  voies,
});

// Zyra : voie inconnue (absente de la source des voies).
const champions = [
  champion("Garen", "Garen", ["top"]),
  champion("LeeSin", "Lee Sin", ["jungle"]),
  champion("Jinx", "Jinx", ["adc"]),
  champion("Zyra"),
];

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
  championsDejaJoues: ["Jinx"],
  resetDemande: null,
};

async function rendreCapitaine() {
  mockApi({
    "GET /champions": champions,
    "GET /liens/lien-a": { seriesId: 5, portee: "Capitaine", equipe: "A" },
    "GET /liens/lien-a/draft-champions": draft,
  });
  renderComponent(<DraftParLien lien="lien-a" portee="Capitaine" />);
  const voies = await screen.findByRole("group", { name: "Filtrer par voie" });
  await screen.findByRole("button", { name: "Lee Sin" });
  return voies;
}

describe("Filtre par voie de la grille de champions", () => {
  it("propose Tous, Top, Jungle, Mid, ADC et Support, sans filtre par défaut", async () => {
    const voies = await rendreCapitaine();

    expect(within(voies).getAllByRole("button").map((b) => b.textContent)).toEqual([
      "Tous",
      "Top",
      "Jungle",
      "Mid",
      "ADC",
      "Support",
    ]);
    expect(within(voies).getByRole("button", { name: "Tous" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "Garen" })).toBeInTheDocument();
  });

  it("ne montre que les champions de la voie, plus ceux sans donnée de voie", async () => {
    const voies = await rendreCapitaine();

    await userEvent.click(within(voies).getByRole("button", { name: "Jungle" }));

    expect(screen.getByRole("button", { name: "Lee Sin" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Zyra" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Garen" })).not.toBeInTheDocument();
    expect(within(voies).getByRole("button", { name: "Jungle" })).toHaveAttribute("aria-pressed", "true");
  });

  it("se combine avec la recherche par nom, et Tous revient à la grille entière", async () => {
    const voies = await rendreCapitaine();

    await userEvent.click(within(voies).getByRole("button", { name: "Top" }));
    const recherche = screen.getByRole("searchbox", { name: "Rechercher un champion" });
    await userEvent.type(recherche, "gar");

    expect(screen.getByRole("button", { name: "Garen" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Zyra" })).not.toBeInTheDocument();

    await userEvent.clear(recherche);
    await userEvent.click(within(voies).getByRole("button", { name: "Tous" }));
    expect(screen.getByRole("button", { name: "Lee Sin" })).toBeInTheDocument();
  });

  it("laisse affiché et marqué, sous sa voie, un champion déjà joué par Fearless", async () => {
    const voies = await rendreCapitaine();

    await userEvent.click(within(voies).getByRole("button", { name: "ADC" }));

    const jinx = screen.getByRole("button", { name: /Jinx/ });
    expect(jinx).toBeEnabled();
    expect(within(jinx).getByText("Joué")).toBeInTheDocument();
  });
});
