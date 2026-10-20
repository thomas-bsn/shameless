import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { evaluer, separer, type LigneAEvaluer } from "../src/evaluation.ts";
import { parametresParDefaut, type Mesures } from "../src/note.ts";

/** Une ligne dont la note par défaut vaut exactement `note` : un seul critère mesuré (KDA), à la bonne valeur entre Faible et Moyen du mid (1 → 3). */
function ligne(matchId: string, numero: number, note: number, score: number, place?: number): LigneAEvaluer {
  const mesures: Mesures = { kda: 1 + (2 * note) / 50 }; // note = 50 * (kda - 1) / 2 pour kda <= 3
  return { matchId, numero, place, echantillon: { mesures, gagnee: undefined, role: "MID", score } };
}

describe("évaluation", () => {
  const lignes = [
    ligne("G1", 1, 75, 80),
    ligne("G1", 2, 65, 60),
    ligne("G1", 3, 40, 40),
    ligne("G2", 4, 40, 70),
    ligne("G2", 5, 60, 50),
  ];

  it("rapporte l'écart moyen en points et la fréquence du même ordre dans la game", () => {
    const r = evaluer(lignes, parametresParDefaut());
    assert.equal(r.lignes, 5);
    assert.ok(Math.abs(r.ecartMoyen - 10) < 1e-9); // (5 + 5 + 0 + 30 + 10) / 5
    assert.deepEqual([r.games, r.gamesDansLeMemeOrdre], [2, 1]);
    assert.equal(r.frequenceDuMemeOrdre, 0.5);
    assert.equal(r.frequenceDesPairesConcordantes, 3 / 4);
  });

  it("utilise la place dpm.lol quand elle est saisie pour toutes les lignes de la game", () => {
    const avecPlaces = [ligne("G2", 4, 40, 70, 2), ligne("G2", 5, 60, 50, 1)]; // la place dit l'inverse du score
    const r = evaluer(avecPlaces, parametresParDefaut());
    assert.equal(r.gamesDansLeMemeOrdre, 1);
  });

  it("ignore pour l'ordre les games d'une seule ligne", () => {
    const r = evaluer([ligne("G1", 1, 75, 80)], parametresParDefaut());
    assert.equal(r.games, 0);
    assert.ok(Number.isNaN(r.frequenceDuMemeOrdre));
    assert.equal(r.lignes, 1);
  });
});

describe("séparation apprentissage / test", () => {
  const lignes = Array.from({ length: 40 }, (_, i) => ligne(`G${Math.floor(i / 4)}`, i + 1, 50, 50));

  it("sépare par game : aucune game n'est des deux côtés", () => {
    const { apprentissage, test } = separer(lignes, 0.25, 7);
    const gamesTest = new Set(test.map((l) => l.matchId));
    assert.equal(gamesTest.size, 3); // 10 games × 0,25 = 2,5 → 3
    assert.ok(apprentissage.every((l) => !gamesTest.has(l.matchId)));
    assert.equal(apprentissage.length + test.length, 40);
  });

  it("est reproductible pour une graine et varie avec elle", () => {
    const a = separer(lignes, 0.3, 1).test.map((l) => l.numero);
    assert.deepEqual(separer(lignes, 0.3, 1).test.map((l) => l.numero), a);
    assert.notDeepEqual(separer(lignes, 0.3, 2).test.map((l) => l.numero), a);
  });

  it("garde toujours au moins une game de chaque côté", () => {
    const deux = [ligne("G1", 1, 50, 50), ligne("G2", 2, 50, 50)];
    const { apprentissage, test } = separer(deux, 0.01, 1);
    assert.equal(apprentissage.length, 1);
    assert.equal(test.length, 1);
  });
});
