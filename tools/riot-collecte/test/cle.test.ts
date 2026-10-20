import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { mkdir, mkdtemp, readFile, readdir, stat, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { after, before, describe, it } from "node:test";
import { fileURLToPath } from "node:url";
import { CLE, demarrerFauxRiot, faireMatch, type FauxRiot } from "./faux-riot.ts";

const MAIN = fileURLToPath(new URL("../src/main.ts", import.meta.url));
const PUUID = "puuid-de-sam";

function lancer(args: string[], env: Record<string, string>) {
  return new Promise<{ code: number | null; sortie: string }>((ok) => {
    const p = spawn(process.execPath, [MAIN, ...args], {
      env: { PATH: process.env.PATH ?? "", ...env },
    });
    let sortie = "";
    p.stdout.on("data", (d) => (sortie += d));
    p.stderr.on("data", (d) => (sortie += d));
    p.on("close", (code) => ok({ code, sortie }));
  });
}

async function tousLesFichiers(dossier: string): Promise<string[]> {
  const res: string[] = [];
  const entrees = await readdir(dossier).catch(() => [] as string[]); // dossier jamais créé : rien à fouiller
  for (const e of entrees) {
    const chemin = join(dossier, e);
    if ((await stat(chemin)).isDirectory()) res.push(...(await tousLesFichiers(chemin)));
    else res.push(chemin);
  }
  return res;
}

async function verifierAbsente(cle: string, dossier: string, sortie: string) {
  assert.ok(!sortie.includes(cle), "la clé apparaît dans la sortie du script");
  for (const f of await tousLesFichiers(dossier)) {
    assert.ok(!(await readFile(f, "utf8")).includes(cle), `la clé apparaît dans ${f}`);
  }
}

describe("clé Riot", () => {
  let riot: FauxRiot;
  before(async () => {
    riot = await demarrerFauxRiot({
      comptes: { "Sam#EUW": PUUID },
      matches: { [PUUID]: [faireMatch("EUW1_1", PUUID, "Zed", Date.now(), 1800), faireMatch("EUW1_2", PUUID, "Ahri", Date.now(), 1900)] },
    });
  });
  after(() => riot.fermer());

  const args = (sortie: string) => ["Sam#EUW", "--nombre", "2", "--dossier", sortie, "--base-url", riot.url];

  it("lue dans RIOT_API_KEY, elle n'apparaît dans aucun fichier ni journal", async () => {
    const home = await mkdtemp(join(tmpdir(), "home-"));
    const sortie = join(await mkdtemp(join(tmpdir(), "out-")), "d");
    const r = await lancer(args(sortie), { HOME: home, RIOT_API_KEY: CLE });
    assert.equal(r.code, 0, r.sortie);
    assert.equal((await readdir(join(sortie, "matches"))).length, 2);
    await verifierAbsente(CLE, sortie, r.sortie);
    await verifierAbsente(CLE, home, "");
  });

  it("lue dans ~/.config/shameless/riot-key quand la variable manque", async () => {
    const home = await mkdtemp(join(tmpdir(), "home-"));
    await mkdir(join(home, ".config", "shameless"), { recursive: true });
    await writeFile(join(home, ".config", "shameless", "riot-key"), CLE + "\n");
    const sortie = join(await mkdtemp(join(tmpdir(), "out-")), "d");
    const r = await lancer(args(sortie), { HOME: home });
    assert.equal(r.code, 0, r.sortie);
    await verifierAbsente(CLE, sortie, r.sortie);
  });

  it("une clé refusée par Riot n'est pas répétée dans l'erreur", async () => {
    const mauvaise = "RGAPI-mauvaise-cle-secrete";
    const home = await mkdtemp(join(tmpdir(), "home-"));
    const sortie = join(await mkdtemp(join(tmpdir(), "out-")), "d");
    const r = await lancer(args(sortie), { HOME: home, RIOT_API_KEY: mauvaise });
    assert.notEqual(r.code, 0);
    assert.match(r.sortie, /403/);
    await verifierAbsente(mauvaise, sortie, r.sortie);
  });

  it("sans clé, s'arrête en disant où la mettre", async () => {
    const home = await mkdtemp(join(tmpdir(), "home-"));
    const sortie = join(await mkdtemp(join(tmpdir(), "out-")), "d");
    const avant = riot.requetes.length;
    const r = await lancer(args(sortie), { HOME: home });
    assert.notEqual(r.code, 0);
    assert.match(r.sortie, /riot-key/);
    assert.match(r.sortie, /RIOT_API_KEY/);
    assert.equal(riot.requetes.length, avant);
  });

  it("refuse d'envoyer la clé ailleurs que chez Riot ou en local", async () => {
    const home = await mkdtemp(join(tmpdir(), "home-"));
    const sortie = join(await mkdtemp(join(tmpdir(), "out-")), "d");
    const r = await lancer(["Sam#EUW", "--nombre", "1", "--dossier", sortie, "--base-url", "https://example.org"], {
      HOME: home,
      RIOT_API_KEY: CLE,
    });
    assert.notEqual(r.code, 0);
    assert.match(r.sortie, /base-url/);
  });
});
