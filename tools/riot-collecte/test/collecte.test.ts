import assert from "node:assert/strict";
import { mkdtemp, readFile, readdir } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { after, before, describe, it } from "node:test";
import { collecter } from "../src/collecte.ts";
import { CLE, demarrerFauxRiot, faireMatch, type FauxRiot } from "./faux-riot.ts";

const PUUID = "puuid-de-sam";
const T0 = Date.UTC(2026, 9, 1, 20, 0, 0);

function parties(n: number) {
  // plus récente d'abord ; une game sur quatre n'est pas de la SoloQ
  return Array.from({ length: n }, (_, i) =>
    faireMatch(`EUW1_${1000 + i}`, PUUID, `Champion${i}`, T0 - i * 3_600_000, 1500 + i, i % 4 === 3 ? 450 : 420),
  );
}

async function dossierVide() {
  return mkdtemp(join(tmpdir(), "collecte-"));
}

const silence = () => {};
const limitesLarges = { parSeconde: 1000, parFenetre: 100000, fenetreMs: 120000 };

describe("collecte des games de SoloQ", () => {
  let riot: FauxRiot;
  const matches = parties(8); // 6 classées (420), 2 autres
  before(async () => {
    riot = await demarrerFauxRiot({ comptes: { "Sam#EUW": PUUID }, matches: { [PUUID]: matches } });
  });
  after(() => riot.fermer());

  it("enregistre chaque game classée en JSON complet, plus un index", async () => {
    const dossier = await dossierVide();
    const r = await collecter({
      riotId: "Sam#EUW",
      nombre: 5,
      dossier,
      cle: CLE,
      baseUrl: riot.url,
      limites: limitesLarges,
      journal: silence,
    });

    const classees = matches.filter((m) => m.queue === 420).slice(0, 5);
    assert.equal(r.telechargees, 5);
    const fichiers = (await readdir(join(dossier, "matches"))).sort();
    assert.deepEqual(fichiers, classees.map((m) => `${m.id}.json`).sort());
    const brut = JSON.parse(await readFile(join(dossier, "matches", `${classees[0]!.id}.json`), "utf8"));
    assert.deepEqual(brut, classees[0]!.doc);

    const index = JSON.parse(await readFile(join(dossier, "index.json"), "utf8"));
    assert.equal(index.cible.puuid, PUUID);
    assert.equal(index.games.length, 5);
    assert.deepEqual(index.games[0], {
      matchId: classees[0]!.id,
      date: new Date(T0).toISOString(),
      duree: 1500,
      champion: "Champion0",
    });
  });
});
