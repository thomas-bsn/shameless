import { screen, within } from "@testing-library/react";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { renderComponent } from "@/test/render";
import { ChampionsJoues } from "./champions-joues";

const participant = (playerId: number, prenom: string): Schemas["GameParticipantDto"] => ({
  prenom,
  riotId: `${prenom}#EUW`,
  rankTier: null,
  rankDivision: null,
  cost: null,
  playerId,
  role: null,
});

const game = (overrides: Partial<Schemas["SeriesGameResponse"]> = {}): Schemas["SeriesGameResponse"] => ({
  id: 7,
  status: "completed",
  teamASide: "red",
  teamBSide: "blue",
  winner: "A",
  teamA: [participant(1, "Alice"), participant(2, "Bruno")],
  teamB: [participant(3, "Chloé"), participant(4, "David")],
  riotMatchId: "EUW1_7000000001",
  championsJoues: [
    { playerId: 1, championId: "LeeSin" },
    { playerId: 2, championId: "Ahri" },
    { playerId: 3, championId: "Garen" },
    { playerId: 4, championId: "Inconnu" },
  ],
  rapportEnRevue: null,
  startedAt: null,
  ...overrides,
});

const champion = (id: string, name: string): Schemas["ChampionResponse"] => ({
  id,
  key: id,
  name,
  imageUrl: `https://ddragon/${id}.png`,
  voies: [],
});

describe("ChampionsJoues", () => {
  beforeEach(() => {
    mockApi({ "GET /champions": [champion("LeeSin", "Lee Sin"), champion("Ahri", "Ahri"), champion("Garen", "Garen")] });
  });

  it("montre le champion réellement joué par chaque joueur, par Équipe", async () => {
    renderComponent(<ChampionsJoues game={game()} />);

    const equipeA = await screen.findByRole("list", { name: "Champions joués par l'Équipe A" });
    const equipeB = screen.getByRole("list", { name: "Champions joués par l'Équipe B" });
    expect(await within(equipeA).findByText("Lee Sin")).toBeInTheDocument();
    expect(within(equipeA).getByText("Alice")).toBeInTheDocument();
    expect(within(equipeA).getByRole("img", { name: "Lee Sin" })).toHaveAttribute("src", "https://ddragon/LeeSin.png");
    expect(within(equipeB).getByText("Chloé")).toBeInTheDocument();
    expect(within(equipeB).getByText("Garen")).toBeInTheDocument();
    expect(within(equipeB).getByText("Inconnu")).toBeInTheDocument();
  });

  it("ne montre rien pour une game sans rapport du Companion", () => {
    const { container } = renderComponent(<ChampionsJoues game={game({ riotMatchId: null, championsJoues: [] })} />);

    expect(container).toBeEmptyDOMElement();
  });
});
