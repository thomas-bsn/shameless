import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { ajuster, erreurMoyenne, type Echantillon } from "../src/ajustement.ts";
import { CRITERES, noterMesures, parametresParDefaut, type Mesures, type Parametres } from "../src/note.ts";

/** Générateur pseudo-aléatoire déterministe (mulberry32). */
function alea(graine: number) {
  let a = graine;
  return () => {
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** Des mesures de mid réparties autour des références de `verite`, notées avec `verite` : le score dpm.lol « idéal ». */
function echantillonsSynthetiques(verite: Parametres, n: number, graine: number): Echantillon[] {
  const hasard = alea(graine);
  const mid = verite.roles.MID.criteres;
  return Array.from({ length: n }, () => {
    const mesures: Mesures = {};
    for (const c of CRITERES) {
      const r = mid[c];
      mesures[c] = r.faible - 0.2 * (r.excellent - r.faible) + hasard() * 1.4 * (r.excellent - r.faible);
    }
    const gagnee = hasard() < 0.5;
    const note = noterMesures(mesures, gagnee, "MID", verite)!.note;
    return { mesures, gagnee, role: "MID" as const, score: note };
  });
}

function poidsNormalises(p: Parametres) {
  const r = p.roles.MID;
  const total = CRITERES.reduce((s, c) => s + r.criteres[c].poids, 0) + r.poidsVictoire;
  return { ...Object.fromEntries(CRITERES.map((c) => [c, (100 * r.criteres[c].poids) / total])), victoire: (100 * r.poidsVictoire) / total };
}

describe("ajustement", () => {
  it("retrouve des poids connus (dont la victoire) sur des données générées avec eux", () => {
    const verite = parametresParDefaut();
    const mid = verite.roles.MID;
    mid.criteres.degatsParMinute.poids = 40; // au lieu de 25
    mid.criteres.csParMinute.poids = 10; // au lieu de 20
    mid.criteres.orParMinute.poids = 15; // critère absent par défaut
    mid.poidsVictoire = 25;
    const donnees = echantillonsSynthetiques(verite, 300, 1);

    const depart = parametresParDefaut();
    const avant = erreurMoyenne(donnees, depart);
    const { parametres, perte } = ajuster(donnees, depart, { ajuster: "poids", iterations: 500 });

    assert.ok(avant > 3, `l'écart de départ doit être sensible (${avant})`);
    assert.ok(perte < 0.05, `écart quadratique moyen final trop grand : ${perte}`);
    const trouves = poidsNormalises(parametres);
    const attendus = poidsNormalises(verite);
    for (const [k, v] of Object.entries(attendus)) {
      assert.ok(Math.abs((trouves as Record<string, number>)[k]! - v) < 1.5, `${k} : ${(trouves as Record<string, number>)[k]} au lieu de ${v}`);
    }
  });

  it("retrouve des références connues sur des données générées avec elles", () => {
    const verite = parametresParDefaut();
    const mid = verite.roles.MID.criteres;
    mid.degatsParMinute = { ...mid.degatsParMinute, faible: 450, moyen: 760, excellent: 1100 }; // 400/700/1000 par défaut
    mid.csParMinute = { ...mid.csParMinute, faible: 4.5, moyen: 6.6, excellent: 9.4 }; // 5/7/9 par défaut
    const donnees = echantillonsSynthetiques(verite, 400, 2);

    const { parametres, perte } = ajuster(donnees, parametresParDefaut(), { ajuster: "references", iterations: 800 });

    assert.ok(perte < 0.5, `écart quadratique moyen final trop grand : ${perte}`);
    for (const c of ["degatsParMinute", "csParMinute"] as const) {
      for (const k of ["faible", "moyen", "excellent"] as const) {
        const attendu = verite.roles.MID.criteres[c][k];
        const trouve = parametres.roles.MID.criteres[c][k];
        assert.ok(Math.abs(trouve - attendu) / attendu < 0.04, `${c}.${k} : ${trouve} au lieu de ${attendu}`);
      }
    }
  });

  it("ne touche ni aux rôles absents des données ni aux paramètres hors du groupe ajusté", () => {
    const verite = parametresParDefaut();
    verite.roles.MID.poidsVictoire = 30;
    const donnees = echantillonsSynthetiques(verite, 100, 3);
    const depart = parametresParDefaut();
    const { parametres } = ajuster(donnees, depart, { ajuster: "poids", iterations: 100 });
    assert.deepEqual(parametres.roles.TOP, depart.roles.TOP);
    assert.deepEqual(parametres.roles.MID.criteres.kda.faible, depart.roles.MID.criteres.kda.faible);
    assert.notEqual(parametres.roles.MID.poidsVictoire, 0);
  });
});
