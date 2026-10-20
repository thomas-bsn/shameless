import assert from "node:assert/strict";
import { mkdtemp, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { describe, it } from "node:test";
import { apparier } from "../src/appariement.ts";
import { analyserScores, lireDossier, lireScores } from "../src/lecture.ts";
import type { LigneDeScore } from "../src/types.ts";
import { ecrireDossier, gameDeTest, T0 } from "./donnees.ts";

const TOL = { date: 600, duree: 30 };

function ligne(numero: number, o: Partial<LigneDeScore> = {}): LigneDeScore {
  return { numero, date: T0, duree: 1800, champion: "A3", kills: 4, deaths: 3, assists: 7, score: 60, ...o };
}

describe("lecture d'un dossier de collecte", () => {
  it("relit les games et la cible", async () => {
    const dossier = await ecrireDossier([gameDeTest("EUW1_1", T0, 1800, "A")]);
    const d = await lireDossier(dossier);
    assert.equal(d.cible?.riotId, "Sam#EUW");
    assert.equal(d.games.length, 1);
    assert.equal(d.games[0]!.matchId, "EUW1_1");
    assert.equal(d.games[0]!.debut, T0);
    assert.equal(d.games[0]!.duree, 1800);
    assert.equal(d.games[0]!.participants.length, 10);
  });
});

describe("lecture du fichier de scores", () => {
  it("lit un CSV (virgule ou point-virgule, durée en mm:ss, date sans fuseau = UTC, place facultative)", () => {
    const csv = [
      "date,duree,champion,kills,deaths,assists,score,place",
      "2026-10-01 20:00,30:00,Zed,4,3,7,61.5,3",
      "2026-10-01T22:00:00Z,1500,Kai'Sa,1,2,3,40,",
    ].join("\n");
    const l = analyserScores(csv, "csv");
    assert.equal(l.length, 2);
    assert.deepEqual(l[0], { numero: 1, date: T0, duree: 1800, champion: "Zed", kills: 4, deaths: 3, assists: 7, score: 61.5, place: 3 });
    assert.equal(l[1]!.date, T0 + 2 * 3600_000);
    assert.equal(l[1]!.duree, 1500);
    assert.equal(l[1]!.place, undefined);

    const pv = analyserScores("date;duree;champion;kills;deaths;assists;score\n2026-10-01 20:00;1800;Zed;4;3;7;61,5", "csv");
    assert.equal(pv[0]!.score, 61.5);
  });

  it("lit un JSON équivalent", () => {
    const json = JSON.stringify([{ date: "2026-10-01T20:00:00Z", duree: 1800, champion: "Zed", kills: 4, deaths: 3, assists: 7, score: 61.5, place: 2 }]);
    const l = analyserScores(json, "json");
    assert.deepEqual(l[0], { numero: 1, date: T0, duree: 1800, champion: "Zed", kills: 4, deaths: 3, assists: 7, score: 61.5, place: 2 });
  });

  it("refuse une ligne invalide en citant son numéro", () => {
    assert.throws(() => analyserScores("date,duree,champion,kills,deaths,assists,score\nn'importe quoi,1800,Zed,4,3,7,60", "csv"), /ligne 1.*date/i);
    assert.throws(() => analyserScores("date,duree,champion,kills,deaths,assists\n", "csv"), /colonne.*score/i);
  });

  it("choisit le format selon l'extension du fichier", async () => {
    const dir = await mkdtemp(join(tmpdir(), "scores-"));
    const chemin = join(dir, "s.json");
    await writeFile(chemin, JSON.stringify([{ date: "2026-10-01T20:00:00Z", duree: 1800, champion: "Zed", kills: 1, deaths: 1, assists: 1, score: 50 }]));
    assert.equal((await lireScores(chemin)).length, 1);
  });
});

describe("appariement", () => {
  const games = [gameDeTest("EUW1_1", T0, 1800, "A"), gameDeTest("EUW1_2", T0 + 3600_000, 1500, "B")];

  it("retrouve la bonne game et le bon joueur, à la tolérance près", () => {
    const r = apparier(
      [ligne(1), ligne(2, { date: T0 + 3600_000 + 120_000, duree: 1510, champion: "B7", kills: 8, deaths: 3, assists: 11 })],
      games,
      TOL,
    );
    assert.equal(r.appariees.length, 2);
    assert.equal(r.appariees[0]!.game.matchId, "EUW1_1");
    assert.equal(r.appariees[0]!.participant.championName, "A3");
    assert.equal(r.appariees[1]!.game.matchId, "EUW1_2");
    assert.equal(r.appariees[1]!.participant.championName, "B7");
    assert.deepEqual([r.ambigues.length, r.sansCorrespondance.length], [0, 0]);
  });

  it("ignore les différences de casse et de ponctuation du champion", () => {
    const g = gameDeTest("EUW1_1", T0, 1800, "A");
    g.participants[2]!.championName = "Kai'Sa";
    const r = apparier([ligne(1, { champion: "kaisa", kills: 3, deaths: 3, assists: 6 })], [g], TOL);
    assert.equal(r.appariees.length, 1);
  });

  it("signale une ligne sans correspondance, avec la raison", () => {
    const r = apparier(
      [
        ligne(1, { date: T0 + 5 * 3600_000 }), // aucune game à cette date
        ligne(2, { duree: 1900 }), // durée hors tolérance
        ligne(3, { kills: 99 }), // game trouvée, joueur introuvable
      ],
      games,
      TOL,
    );
    assert.equal(r.appariees.length, 0);
    assert.deepEqual(r.sansCorrespondance.map((s) => s.ligne.numero), [1, 2, 3]);
    assert.match(r.sansCorrespondance[0]!.raison, /game/i);
    assert.match(r.sansCorrespondance[2]!.raison, /joueur/i);
  });

  it("signale une ligne ambiguë : deux games proches avec le même joueur", () => {
    const g1 = gameDeTest("EUW1_1", T0, 1800, "A");
    const g2 = gameDeTest("EUW1_2", T0 + 60_000, 1805, "A");
    const r = apparier([ligne(1)], [g1, g2], TOL);
    assert.equal(r.appariees.length, 0);
    assert.equal(r.ambigues.length, 1);
    assert.deepEqual(r.ambigues[0]!.candidats.map((c) => c.game.matchId).sort(), ["EUW1_1", "EUW1_2"]);
  });

  it("signale deux lignes qui visent le même joueur de la même game", () => {
    const r = apparier([ligne(1), ligne(2)], games, TOL);
    assert.equal(r.appariees.length, 0);
    assert.deepEqual(r.ambigues.map((a) => a.ligne.numero), [1, 2]);
  });
});
