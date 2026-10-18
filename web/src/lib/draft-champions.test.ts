import type { Schemas } from "@/api/client";
import {
  championsPris,
  filtrerChampions,
  libelleTour,
  mainALOrganisateur,
  peutJouer,
  secondesRestantes,
  toursDeLaDraft,
} from "./draft-champions";

const champion = (id: string, name: string = id): Schemas["ChampionResponse"] => ({
  id,
  key: id,
  name,
  imageUrl: `https://ddragon.example/${id}.png`,
  classes: [],
});

const action = (numero: number, championId: string): Schemas["ActionDraftResponse"] => ({
  numero,
  action: numero <= 6 ? "ban" : "pick",
  side: "blue",
  equipe: "A",
  championId,
});

const at = (iso: string) => new Date(iso).getTime();

describe("secondesRestantes", () => {
  it("compte jusqu'à l'échéance du tour quand l'API la donne", () => {
    const tour = { echeance: "2026-10-16T20:00:45Z" };
    expect(secondesRestantes(tour, at("2026-10-16T20:00:30Z"))).toBe(15);
  });

  it("arrondit à la seconde supérieure et ne descend pas sous zéro", () => {
    const tour = { echeance: "2026-10-16T20:01:00Z" };
    expect(secondesRestantes(tour, at("2026-10-16T20:00:10.400Z"))).toBe(50);
    expect(secondesRestantes(tour, at("2026-10-16T20:02:00Z"))).toBe(0);
  });
});

// L'ordre Tournament tel que l'API le donne, Équipe B côté blue :
// bans B1 R1 B2 R2 B3 R3, picks B1 R1 R2 B2 B3 R3, bans R4 B4 R5 B5, picks R4 B4 B5 R5.
const ordre: [string, string][] = [
  ["ban", "blue"], ["ban", "red"], ["ban", "blue"], ["ban", "red"], ["ban", "blue"], ["ban", "red"],
  ["pick", "blue"], ["pick", "red"], ["pick", "red"], ["pick", "blue"], ["pick", "blue"], ["pick", "red"],
  ["ban", "red"], ["ban", "blue"], ["ban", "red"], ["ban", "blue"],
  ["pick", "red"], ["pick", "blue"], ["pick", "blue"], ["pick", "red"],
];
const tours: Schemas["TourDraftResponse"][] = ordre.map(([action, side], i) => ({
  numero: i + 1,
  action,
  side,
  equipe: side === "blue" ? "B" : "A",
}));

describe("libelleTour", () => {
  it("nomme chaque tour de l'ordre Tournament par action, Side et rang dans ce Side", () => {
    expect(libelleTour(tours, 1)).toBe("Ban blue 1");
    expect(libelleTour(tours, 6)).toBe("Ban red 3");
    expect(libelleTour(tours, 7)).toBe("Pick blue 1");
    expect(libelleTour(tours, 9)).toBe("Pick red 2");
    expect(libelleTour(tours, 13)).toBe("Ban red 4");
    expect(libelleTour(tours, 14)).toBe("Ban blue 4");
    expect(libelleTour(tours, 17)).toBe("Pick red 4");
    expect(libelleTour(tours, 20)).toBe("Pick red 5");
  });
});

type Draft = Pick<Schemas["DraftChampionsResponse"], "terminee" | "tourCourant" | "tours" | "actions">;

const enCours = (tour: number, equipe: string, actions: Schemas["ActionDraftResponse"][] = []): Draft => ({
  tours,
  terminee: false,
  tourCourant: { numero: tour, action: "ban", side: "blue", equipe, commenceLe: "2026-10-16T20:00:00Z", echeance: "2026-10-16T20:01:00Z" },
  actions,
});

describe("peutJouer", () => {
  it("laisse jouer le capitaine de l'Équipe attendue au tour courant", () => {
    expect(peutJouer(enCours(1, "B"), "B")).toBe(true);
  });

  it("refuse l'autre Équipe, le spectateur et une Draft terminée", () => {
    expect(peutJouer(enCours(1, "B"), "A")).toBe(false);
    expect(peutJouer(enCours(1, "B"), null)).toBe(false);
    expect(peutJouer({ ...enCours(1, "B"), terminee: true, tourCourant: null }, "B")).toBe(false);
  });
});

describe("mainALOrganisateur", () => {
  it("laisse le tour au capitaine jusqu'à l'échéance", () => {
    expect(mainALOrganisateur(enCours(1, "B"), at("2026-10-16T20:00:59.900Z"))).toBe(false);
  });

  it("donne la main à l'organisateur dès l'échéance atteinte", () => {
    expect(mainALOrganisateur(enCours(1, "B"), at("2026-10-16T20:01:00Z"))).toBe(true);
    expect(mainALOrganisateur(enCours(1, "B"), at("2026-10-16T20:03:00Z"))).toBe(true);
  });

  it("ne donne rien à jouer quand la Draft est terminée", () => {
    const terminee = { ...enCours(1, "B"), terminee: true, tourCourant: null };
    expect(mainALOrganisateur(terminee, at("2026-10-16T20:03:00Z"))).toBe(false);
  });
});

describe("toursDeLaDraft", () => {
  it("place les tours joués et le tour courant dans l'ordre Tournament, avec l'Équipe de chaque Side", () => {
    const tours = toursDeLaDraft(enCours(3, "B", [action(1, "Zed"), action(2, "Ahri")]));

    expect(tours).toHaveLength(20);
    expect(tours[0]).toEqual({
      numero: 1,
      action: "ban",
      side: "blue",
      equipe: "B",
      libelle: "Ban blue 1",
      championId: "Zed",
      courant: false,
    });
    expect(tours[1]).toMatchObject({ side: "red", equipe: "A", championId: "Ahri", courant: false });
    expect(tours[2]).toMatchObject({ numero: 3, championId: null, courant: true });
    expect(tours[19]).toMatchObject({ numero: 20, action: "pick", side: "red", equipe: "A", courant: false });
  });
});

describe("championsPris", () => {
  it("rend indisponibles les champions déjà bannis ou pick dans la game", () => {
    const pris = championsPris([action(1, "Zed"), action(7, "LeeSin")]);
    expect(pris.has("Zed")).toBe(true);
    expect(pris.has("LeeSin")).toBe(true);
    expect(pris.has("Ahri")).toBe(false);
  });
});

describe("filtrerChampions", () => {
  const catalogue = [champion("MonkeyKing", "Wukong"), champion("Ahri"), champion("KSante", "K'Santé"), champion("Akali")];

  it("trie par nom sans recherche", () => {
    expect(filtrerChampions(catalogue, "").map((c) => c.name)).toEqual(["Ahri", "Akali", "K'Santé", "Wukong"]);
  });

  it("cherche dans le nom, sans tenir compte de la casse, des accents ni de la ponctuation", () => {
    expect(filtrerChampions(catalogue, "AK").map((c) => c.id)).toEqual(["Akali"]);
    expect(filtrerChampions(catalogue, "ksante").map((c) => c.id)).toEqual(["KSante"]);
    expect(filtrerChampions(catalogue, "  wu ").map((c) => c.id)).toEqual(["MonkeyKing"]);
  });

  it("cherche aussi dans l'identifiant Data Dragon", () => {
    expect(filtrerChampions(catalogue, "monkey").map((c) => c.id)).toEqual(["MonkeyKing"]);
  });
});
