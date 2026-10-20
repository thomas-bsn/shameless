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
const limitesLarges = [{ max: 100000, fenetreMs: 1000 }];

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

  it("reprend : une game déjà enregistrée n'est pas retéléchargée", async () => {
    const dossier = await dossierVide();
    const base = { riotId: "Sam#EUW", dossier, cle: CLE, baseUrl: riot.url, limites: limitesLarges, journal: silence };
    await collecter({ ...base, nombre: 3 });
    const avant = riot.requetes.length;

    const r = await collecter({ ...base, nombre: 5 });

    const telechargees = riot.requetes.slice(avant).filter((q) => /\/matches\/EUW1_\d+$/.test(q.chemin));
    assert.equal(telechargees.length, 2);
    assert.equal(r.telechargees, 2);
    assert.equal(r.dejaPresentes, 3);
    const index = JSON.parse(await readFile(join(dossier, "index.json"), "utf8"));
    assert.equal(index.games.length, 5);
  });

  it("parcourt plusieurs pages d'identifiants au-delà de 100 games", async () => {
    const beaucoup = parties(130).map((m) => ({ ...m, queue: 420 }));
    const grand = await demarrerFauxRiot({ comptes: { "Sam#EUW": PUUID }, matches: { [PUUID]: beaucoup } });
    try {
      const dossier = await dossierVide();
      const r = await collecter({ riotId: "Sam#EUW", nombre: 120, dossier, cle: CLE, baseUrl: grand.url, limites: limitesLarges, journal: silence });
      assert.equal(r.telechargees, 120);
    } finally {
      await grand.fermer();
    }
  });
});

describe("limites de débit et erreur 429", () => {
  it("ne dépasse jamais les limites déclarées, sur aucune fenêtre glissante", async () => {
    const m = parties(12).map((g) => ({ ...g, queue: 420 }));
    const riot = await demarrerFauxRiot({ comptes: { "Sam#EUW": PUUID }, matches: { [PUUID]: m } });
    try {
      const limites = [{ max: 4, fenetreMs: 300 }];
      await collecter({ riotId: "Sam#EUW", nombre: 10, dossier: await dossierVide(), cle: CLE, baseUrl: riot.url, limites, journal: silence });
      const t = riot.requetes.map((q) => q.temps);
      assert.equal(t.length, 12); // compte + liste + 10 games
      for (let i = 0; i + 4 < t.length; i++) {
        assert.ok(t[i + 4]! - t[i]! >= 300, `5 requêtes en ${t[i + 4]! - t[i]!} ms`);
      }
    } finally {
      await riot.fermer();
    }
  });

  it("attend le Retry-After après un 429 puis reprend sans rien perdre", async () => {
    const m = parties(4).map((g) => ({ ...g, queue: 420 }));
    const riot = await demarrerFauxRiot({ comptes: { "Sam#EUW": PUUID }, matches: { [PUUID]: m } });
    try {
      riot.refuser429(2, 1);
      const debut = Date.now();
      const dossier = await dossierVide();
      const r = await collecter({ riotId: "Sam#EUW", nombre: 4, dossier, cle: CLE, baseUrl: riot.url, limites: limitesLarges, journal: silence });
      assert.equal(r.telechargees, 4);
      // deux refus d'une seconde chacun, respectés l'un après l'autre
      assert.ok(Date.now() - debut >= 2000, "Retry-After non respecté");
      const t = riot.requetes.map((q) => q.temps);
      assert.ok(t[1]! - t[0]! >= 1000 && t[2]! - t[1]! >= 1000);
      assert.equal((await readdir(join(dossier, "matches"))).length, 4);
    } finally {
      await riot.fermer();
    }
  });

  it("abandonne avec un message clair si Riot refuse sans cesse", async () => {
    const riot = await demarrerFauxRiot({ comptes: { "Sam#EUW": PUUID }, matches: { [PUUID]: [] } });
    try {
      riot.refuser429(1000, 0);
      await assert.rejects(
        collecter({ riotId: "Sam#EUW", nombre: 1, dossier: await dossierVide(), cle: CLE, baseUrl: riot.url, limites: limitesLarges, journal: silence }),
        /429/,
      );
    } finally {
      await riot.fermer();
    }
  });
});
