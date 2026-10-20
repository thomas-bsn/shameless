import { csParMinute, csTotal, dureeLabel, estSaisieAMain, kdaLabel, millierLabel, noteTone, posteLabel, ratioKda, statLabel, trierParRole } from "./game";

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

describe("trierParRole", () => {
  const joueur = (nom: string, position: string | null) => ({ nom, stats: position === null ? null : { position } });

  it("range les joueurs Top, Jungle, Mid, ADC, Support, quel que soit leur ordre d'enregistrement", () => {
    const ordre = trierParRole([
      joueur("Charles", "Top"),
      joueur("Fares", "Jungle"),
      joueur("Qao", "Support"),
      joueur("Thomas", "Mid"),
      joueur("Zoubir", "Adc"),
    ]).map((j) => j.nom);
    expect(ordre).toEqual(["Charles", "Fares", "Thomas", "Zoubir", "Qao"]);
  });

  it("garde l'ordre d'origine quand aucun rôle n'est connu", () => {
    const joueurs = [joueur("A", null), joueur("B", null), joueur("C", null)];
    expect(trierParRole(joueurs).map((j) => j.nom)).toEqual(["A", "B", "C"]);
  });

  it("met les joueurs sans rôle après ceux qui en ont un, dans leur ordre", () => {
    const ordre = trierParRole([joueur("Sans1", null), joueur("Mid", "Mid"), joueur("Sans2", null), joueur("Top", "Top")]).map((j) => j.nom);
    expect(ordre).toEqual(["Top", "Mid", "Sans1", "Sans2"]);
  });

  it("ne modifie pas la liste reçue", () => {
    const joueurs = [joueur("Support", "Support"), joueur("Top", "Top")];
    trierParRole(joueurs);
    expect(joueurs.map((j) => j.nom)).toEqual(["Support", "Top"]);
  });
});

describe("ratioKda", () => {
  it("vaut (kills + assists) / morts, avec au moins une mort", () => {
    expect(ratioKda({ kills: 41, deaths: 3, assists: 3 })).toBeCloseTo(14.67, 2);
    expect(ratioKda({ kills: 5, deaths: 0, assists: 2 })).toBe(7);
  });

  it("est inconnu quand une stat manque", () => {
    expect(ratioKda({ kills: 5, deaths: null, assists: 2 })).toBeNull();
  });
});

describe("csParMinute", () => {
  it("divise sbires et monstres par les minutes jouées", () => {
    expect(csParMinute({ minionsKilled: 150, neutralMinionsKilled: 25 }, 1500)).toBe(7);
  });

  it("est inconnu sans CS ou sans durée", () => {
    expect(csParMinute({ minionsKilled: null, neutralMinionsKilled: null }, 1500)).toBeNull();
    expect(csParMinute({ minionsKilled: 150, neutralMinionsKilled: null }, null)).toBeNull();
  });
});
