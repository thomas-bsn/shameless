import { csTotal, dureeLabel, estSaisieAMain, kdaLabel, millierLabel, noteTone, posteLabel, statLabel } from "./game";

describe("dureeLabel", () => {
  it("écrit une durée en minutes:secondes", () => {
    expect(dureeLabel(1834)).toBe("30:34");
    expect(dureeLabel(65)).toBe("1:05");
  });
});

describe("kdaLabel", () => {
  it("écrit kills/morts/assists, avec un tiret pour une stat inconnue", () => {
    expect(kdaLabel({ kills: 7, deaths: 2, assists: 9 })).toBe("7/2/9");
    expect(kdaLabel({ kills: 1, deaths: null, assists: null })).toBe("1/–/–");
  });
});

describe("statLabel", () => {
  it("écrit une stat, et « – » quand elle est absente (jamais 0)", () => {
    expect(statLabel(12)).toBe("12");
    expect(statLabel(0)).toBe("0");
    expect(statLabel(null)).toBe("–");
  });
});

describe("millierLabel", () => {
  it("écrit un total en milliers à la française, « – » s'il est absent", () => {
    expect(millierLabel(14200)).toBe("14,2 k");
    expect(millierLabel(null)).toBe("–");
  });
});

describe("csTotal", () => {
  it("additionne sbires et monstres connus, absent si rien n'est connu", () => {
    expect(csTotal({ minionsKilled: 180, neutralMinionsKilled: 20 })).toBe(200);
    expect(csTotal({ minionsKilled: 180, neutralMinionsKilled: null })).toBe(180);
    expect(csTotal({ minionsKilled: null, neutralMinionsKilled: null })).toBeNull();
  });
});

describe("posteLabel", () => {
  it("écrit le poste joué, absent quand il est inconnu", () => {
    expect(posteLabel("Adc")).toBe("ADC");
    expect(posteLabel("Mid")).toBe("Mid");
    expect(posteLabel(null)).toBeNull();
  });
});

describe("noteTone", () => {
  it("classe la note sur 100 : bonne, moyenne, mauvaise", () => {
    expect(noteTone(75)).toBe("good");
    expect(noteTone(50)).toBe("mid");
    expect(noteTone(49)).toBe("bad");
  });
});

describe("estSaisieAMain", () => {
  const sans = { stats: null, performance: null };

  it("reconnaît une game terminée sans aucune stat de joueur", () => {
    expect(estSaisieAMain({ winner: "A", teamA: [sans], teamB: [sans] })).toBe(true);
  });

  it("ne la confond pas avec une game en cours ou avec stats", () => {
    expect(estSaisieAMain({ winner: null, teamA: [sans], teamB: [sans] })).toBe(false);
    expect(estSaisieAMain({ winner: "A", teamA: [{ stats: {} as never, performance: null }], teamB: [sans] })).toBe(false);
  });
});
