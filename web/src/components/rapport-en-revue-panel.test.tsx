import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { json, mockApi, problem } from "@/test/api";
import { renderComponent } from "@/test/render";
import { RapportEnRevuePanel } from "./rapport-en-revue-panel";

const participant = (playerId: number, prenom: string): Schemas["GameParticipantDto"] => ({
  prenom,
  riotId: `${prenom}#EUW`,
  rankTier: null,
  rankDivision: null,
  cost: null,
  playerId,
  role: null,
});

const revue = (overrides: Partial<Schemas["RapportEnRevueResponse"]> = {}): Schemas["RapportEnRevueResponse"] => ({
  id: 12,
  riotMatchId: "EUW1_8000000001",
  recuLe: "2026-10-16T20:00:00",
  equipeGagnante: "B",
  absents: [participant(5, "Elsa")],
  inattendus: [
    { puuid: "puuid-X1", side: "blue", championId: "Thresh", joueur: participant(11, "Xavier") },
    { puuid: "puuid-inconnu-1234567890", side: "red", championId: "Ahri", joueur: null },
  ],
  mauvaisSide: [participant(2, "Bruno")],
  ...overrides,
});

const champions = [{ id: "Thresh", key: "412", name: "Thresh", imageUrl: "https://ddragon/Thresh.png", voies: ["support"] }];

describe("RapportEnRevuePanel", () => {
  it("montre l'écart entre les joueurs détectés et la composition de la game", async () => {
    mockApi({ "GET /champions": champions });

    renderComponent(<RapportEnRevuePanel revue={revue()} numero={2} onDecided={vi.fn()} />);

    expect(await screen.findByRole("region", { name: "Rapport de la game 2 en revue" })).toBeInTheDocument();
    const absents = screen.getByRole("list", { name: "Joueurs attendus absents" });
    expect(within(absents).getByText("Elsa")).toBeInTheDocument();
    const inattendus = screen.getByRole("list", { name: "Joueurs détectés inattendus" });
    expect(within(inattendus).getByText("Xavier")).toBeInTheDocument();
    expect(within(inattendus).getByText(/puuid-inconnu/)).toBeInTheDocument();
    expect(await within(inattendus).findByText("Thresh")).toBeInTheDocument();
    const mauvaisSide = screen.getByRole("list", { name: "Joueurs détectés sur l'autre Side" });
    expect(within(mauvaisSide).getByText("Bruno")).toBeInTheDocument();
    expect(screen.getByText(/l'Équipe B gagne la game/)).toBeInTheDocument();
  });

  it("n'affiche pas les catégories d'écart vides", async () => {
    mockApi({ "GET /champions": champions });

    renderComponent(<RapportEnRevuePanel revue={revue({ absents: [], inattendus: [] })} numero={1} onDecided={vi.fn()} />);

    expect(await screen.findByRole("region", { name: "Rapport de la game 1 en revue" })).toBeInTheDocument();
    expect(screen.queryByRole("list", { name: "Joueurs attendus absents" })).not.toBeInTheDocument();
    expect(screen.queryByRole("list", { name: "Joueurs détectés inattendus" })).not.toBeInTheDocument();
    expect(screen.getByRole("list", { name: "Joueurs détectés sur l'autre Side" })).toBeInTheDocument();
  });

  it("accepte le rapport puis relit la Série", async () => {
    const accepter = vi.fn(() =>
      json({ seriesId: 5, gameId: 7, statut: "Accepte", winner: "B", teamAWins: 0, teamBWins: 1, outcome: "EnCours" }),
    );
    mockApi({ "GET /champions": champions, "POST /rapports/12/accepter": accepter });
    const onDecided = vi.fn();
    const user = userEvent.setup();

    renderComponent(<RapportEnRevuePanel revue={revue()} numero={1} onDecided={onDecided} />);
    await user.click(await screen.findByRole("button", { name: "Accepter le rapport" }));

    await vi.waitFor(() => expect(onDecided).toHaveBeenCalled());
    expect(accepter).toHaveBeenCalledOnce();
  });

  it("rejette le rapport après confirmation", async () => {
    const rejeter = vi.fn(() =>
      json({ seriesId: 5, gameId: 7, statut: "Rejete", winner: null, teamAWins: 0, teamBWins: 0, outcome: "EnCours" }),
    );
    mockApi({ "GET /champions": champions, "POST /rapports/12/rejeter": rejeter });
    vi.spyOn(window, "confirm").mockReturnValue(true);
    const onDecided = vi.fn();
    const user = userEvent.setup();

    renderComponent(<RapportEnRevuePanel revue={revue()} numero={1} onDecided={onDecided} />);
    await user.click(await screen.findByRole("button", { name: "Rejeter le rapport" }));

    await vi.waitFor(() => expect(onDecided).toHaveBeenCalled());
    expect(rejeter).toHaveBeenCalledOnce();
  });

  it("affiche le refus de l'API", async () => {
    mockApi({
      "GET /champions": champions,
      "POST /rapports/12/accepter": problem(409, "Ce rapport n'est plus en revue : l'organisateur a déjà décidé."),
    });
    const user = userEvent.setup();

    renderComponent(<RapportEnRevuePanel revue={revue()} numero={1} onDecided={vi.fn()} />);
    await user.click(await screen.findByRole("button", { name: "Accepter le rapport" }));

    expect(await screen.findByText(/a déjà décidé/)).toBeInTheDocument();
  });
});
