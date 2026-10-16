import type { Schemas } from "@/api/client";
import { ajouterCandidats, joueursLibres, parRole, pickerStep } from "./picker";

type Picker = Schemas["PickerResponse"];
type Participant = Schemas["GameParticipantDto"];

const participant = (playerId: number, overrides: Partial<Participant> = {}): Participant => ({
  playerId,
  prenom: `Joueur ${playerId}`,
  riotId: `J${playerId}#EUW`,
  rankTier: null,
  rankDivision: null,
  cost: 3,
  role: null,
  ...overrides,
});

const ten = Array.from({ length: 10 }, (_, i) => participant(i + 1));

const picker = (overrides: Partial<Picker> = {}): Picker => ({
  players: ten,
  methode: null,
  variante: null,
  teamA: [],
  teamB: [],
  teamACost: 0,
  teamBCost: 0,
  ecartCost: 0,
  canValidate: false,
  capitaineA: null,
  capitaineB: null,
  premierPick: null,
  picks: [],
  equipeAuTrait: null,
  ...overrides,
});

describe("joueursLibres", () => {
  it("leaves out the captains and the players already picked", () => {
    const draft = picker({
      methode: "Draft",
      capitaineA: 1,
      capitaineB: 2,
      teamA: [participant(1), participant(5)],
      teamB: [participant(2)],
      picks: [{ playerId: 5, equipe: "A" }],
      premierPick: "A",
      equipeAuTrait: "B",
    });

    expect(joueursLibres(draft).map((p) => p.playerId)).toEqual([3, 4, 6, 7, 8, 9, 10]);
  });
});

describe("parRole", () => {
  it("orders a team from top to support once the Autorole has given the roles", () => {
    const team = [
      participant(1, { role: "Support" }),
      participant(2, { role: "Mid" }),
      participant(3, { role: "Top" }),
      participant(4, { role: "Adc" }),
      participant(5, { role: "Jungle" }),
    ];

    expect(parRole(team).map((p) => p.playerId)).toEqual([3, 5, 2, 4, 1]);
  });

  it("keeps the team order while there are no roles", () => {
    const team = [participant(4), participant(1), participant(9)];

    expect(parRole(team).map((p) => p.playerId)).toEqual([4, 1, 9]);
  });
});

describe("pickerStep", () => {
  it("starts with the selection of the ten when there is no Picker session", () => {
    expect(pickerStep(null)).toBe("selection");
  });

  it("asks for a method once the ten are selected", () => {
    expect(pickerStep(picker())).toBe("methode");
  });

  it("stays on the Draft while a team is on the clock", () => {
    expect(
      pickerStep(picker({ methode: "Draft", capitaineA: 1, capitaineB: 2, premierPick: "A", equipeAuTrait: "A" })),
    ).toBe("draft");
  });

  it("shows the formed teams once a Roulette is drawn or the Draft is complete", () => {
    expect(pickerStep(picker({ methode: "Roulette", variante: "Equilibree", canValidate: true }))).toBe("equipes");
    expect(pickerStep(picker({ methode: "Draft", premierPick: "B", canValidate: true, equipeAuTrait: null }))).toBe(
      "equipes",
    );
  });
});

describe("ajouterCandidats", () => {
  it("coche les joueurs en vocal qui ne le sont pas encore, après ceux déjà cochés", () => {
    expect(ajouterCandidats([3, 1], [1, 7, 8])).toEqual([3, 1, 7, 8]);
  });

  it("s'arrête à dix joueurs cochés", () => {
    const neuf = [1, 2, 3, 4, 5, 6, 7, 8, 9];
    expect(ajouterCandidats(neuf, [20, 21])).toEqual([...neuf, 20]);
  });
});
