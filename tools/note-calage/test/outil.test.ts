import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { mkdtemp, readFile, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { describe, it } from "node:test";
import { fileURLToPath } from "node:url";
import { noter, parametresParDefaut } from "../src/note.ts";
import type { Game } from "../src/types.ts";
import { ecrireDossier, gameDeTest, T0 } from "./donnees.ts";

const MAIN = fileURLToPath(new URL("../src/main.ts", import.meta.url));

function lancer(args: string[]) {
  return new Promise<{ code: number | null; sortie: string }>((ok) => {
    const p = spawn(process.execPath, [MAIN, ...args], { env: { PATH: process.env.PATH ?? "" } });
    let sortie = "";
    p.stdout.on("data", (d) => (sortie += d));
    p.stderr.on("data", (d) => (sortie += d));
    p.on("close", (code) => ok({ code, sortie }));
  });
}

describe("l'outil sur un dossier et des scores de test", () => {
  it("apparie, ajuste, évalue sur les games de côté et écrit les paramètres", async () => {
    // 12 games de profils variés ; le « dpm.lol » de test pèse beaucoup la victoire.
    const verite = parametresParDefaut();
    for (const r of Object.values(verite.roles)) r.poidsVictoire = 60;
    const games: Game[] = Array.from({ length: 12 }, (_, g) => {
      const jeu = gameDeTest(`EUW1_${g}`, T0 + g * 3600_000, 1500 + 60 * g, `G${g}x`);
      jeu.participants.forEach((p, i) => {
        p.totalDamageDealtToChampions = 8000 + ((i * 7 + g * 3) % 10) * 3000;
        p.visionScore = 10 + ((i * 3 + g) % 7) * 8;
        p.totalMinionsKilled = 90 + ((i * 5 + g * 2) % 9) * 20;
      });
      return jeu;
    });
    const lignes = games.flatMap((g) =>
      g.participants.slice(0, 5).map((p) => {
        const score = noter(g, p, verite).note.toFixed(1);
        const duree = `${Math.floor(g.duree / 60)}:${String(g.duree % 60).padStart(2, "0")}`;
        return `${new Date(g.debut + 30_000).toISOString()},${duree},${p.championName},${p.kills},${p.deaths},${p.assists},${score}`;
      }),
    );
    const dir = await ecrireDossier(games);
    const scores = join(await mkdtemp(join(tmpdir(), "scores-")), "dpm.csv");
    await writeFile(scores, ["date,duree,champion,kills,deaths,assists,score", ...lignes, "2020-01-01T00:00:00Z,20:00,Zed,1,1,1,50"].join("\n"));
    const sortie = join(await mkdtemp(join(tmpdir(), "sortie-")), "params.json");

    const r = await lancer(["--dossier", dir, "--scores", scores, "--sortie", sortie, "--ajuster", "poids", "--iterations", "300"]);

    assert.equal(r.code, 0, r.sortie);
    assert.match(r.sortie, /60 appariées, 0 ambiguës, 1 sans correspondance/);
    assert.match(r.sortie, /ligne 61 .*sans correspondance/);
    const res = JSON.parse(await readFile(sortie, "utf8"));
    assert.ok(res.avantTest.ecartMoyen > 5, `écart de départ : ${res.avantTest.ecartMoyen}`);
    assert.ok(res.apresTest.ecartMoyen < res.avantTest.ecartMoyen / 3, `après : ${res.apresTest.ecartMoyen}`);
    assert.ok(res.apresTest.lignes > 0 && res.apresApprentissage.lignes > res.apresTest.lignes);
    assert.ok(res.parametres.roles.MID.poidsVictoire > 0);
  });

  it("explique l'usage sans arguments", async () => {
    const r = await lancer([]);
    assert.equal(r.code, 1);
    assert.match(r.sortie, /Usage/);
  });
});
