// Vérifie que compose.yaml reste le même fichier pour Docker Compose et pour Forgeyard (ADR 0015) :
// ce que Forgeyard accepte d'un projet (docs/apps/projects.md de Forgeyard), sans modification.
//
// Usage : node tests/compose/forgeyard-compat.mjs        (Docker Compose v2 requis, pas de démon)
//   FORGEYARD_DIR=~/dev/forgeyard  joue en plus le vrai analyseur de Forgeyard (Go requis).
import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { readFileSync, rmSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const racine = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const compose = (...args) =>
  spawnSync("docker", ["compose", "--env-file", "/dev/null", "-f", join(racine, "compose.yaml"), ...args], { encoding: "utf8" });

// Normalisé par Compose, sans variable d'environnement : ce que voit Forgeyard avec ses valeurs par défaut.
const r = compose("config", "--format", "json");
assert.equal(r.status, 0, r.stderr);
assert.doesNotMatch(r.stderr, /variable is not set|is not set/i, "une variable sans valeur par défaut : Forgeyard la refuse");
const projet = JSON.parse(r.stdout);

// Clés de service que Forgeyard prend en charge (`networks` est ajouté par la normalisation de Compose).
const CLES = new Set(["image", "command", "entrypoint", "environment", "volumes", "depends_on", "healthcheck", "restart", "networks"]);
const SERVICE = /^[a-z0-9]([a-z0-9-]{0,30}[a-z0-9])?$/;
let ok = 0;
const verifie = (message, condition) => {
  assert.ok(condition, message);
  console.log(`ok   ${message}`);
  ok++;
};

const services = Object.entries(projet.services);
verifie("20 services au plus", services.length <= 20);
for (const [nom, s] of services) {
  verifie(`${nom} : nom de service valide`, SERVICE.test(nom));
  verifie(`${nom} : image publiée, pas de build`, typeof s.image === "string" && !s.build);
  const inconnues = Object.keys(s).filter((k) => !CLES.has(k) && !k.startsWith("x-"));
  verifie(`${nom} : seulement des clés prises en charge${inconnues.length ? ` (refusées : ${inconnues})` : ""}`, inconnues.length === 0);
  for (const v of s.volumes ?? [])
    verifie(`${nom} : volume nommé ${v.source}`, v.type === "volume" && v.source && !v.source.startsWith("/") && Object.hasOwn(projet.volumes, v.source));
  if (s.restart) verifie(`${nom} : restart ${s.restart}`, /^(no|always|unless-stopped|on-failure(:[0-9]+)?)$/.test(s.restart));
  for (const [dep, d] of Object.entries(s.depends_on ?? {})) {
    verifie(`${nom} : dépend de ${dep} (${d.condition})`, ["service_started", "service_healthy", "service_completed_successfully"].includes(d.condition));
  }
}
for (const [nom, v] of Object.entries(projet.volumes))
  verifie(`volume ${nom} : déclaré sans options`, !v || Object.keys(v).filter((k) => k !== "name").length === 0);
const autresRacines = Object.keys(projet).filter((k) => !["name", "services", "volumes", "networks"].includes(k) && !k.startsWith("x-"));
verifie("fichier : seulement services et volumes à la racine", autresRacines.length === 0);

// `docker compose config` retire les extensions `x-` : on lit donc le fichier brut pour x-forgeyard.
const brut = readFileSync(join(racine, "compose.yaml"), "utf8");
const bloc = (service) => new RegExp(`^  ${service}:\\n(?:(?!^  \\S).*\\n?)*`, "m").exec(brut)?.[0] ?? "";
verifie("un seul service exposé : web, sur le port 80", (brut.match(/expose:\s*\{/g) ?? []).length === 1 && /x-forgeyard:\s*\n\s*expose: \{ port: 80 \}/.test(bloc("web")));
verifie("la base est sauvegardée par pg_dump", /x-forgeyard:\s*\n\s*backup: \{ type: postgres \}/.test(bloc("postgres")));
verifie("la base déclare POSTGRES_USER et POSTGRES_DB (lus par pg_dump)", projet.services.postgres.environment?.POSTGRES_USER && projet.services.postgres.environment?.POSTGRES_DB);

// Aucun service n'attend un autre par depends_on : le wizard est joignable dès le premier `up`.
verifie("pas de depends_on : un `up -d` ne bloque pas avant le wizard", services.every(([, s]) => !s.depends_on));
// Le wizard est la seule porte d'entrée avant l'installation : le service doit exister.
verifie("le service wizard existe et ne redémarre pas", projet.services.wizard?.restart === "no");

// Les ports locaux viennent d'ailleurs, et Compose les publie bien.
const ports = JSON.parse(execFileSync("docker", ["compose", "--env-file", "/dev/null", "-f", join(racine, "compose.yaml"), "-f", join(racine, "compose.ports.yaml"), "config", "--format", "json"], { encoding: "utf8" }));
verifie("compose.ports.yaml publie web (8080:80, 8443:443)", ports.services.web.ports.map((p) => `${p.published}:${p.target}`).join() === "8080:80,8443:443");

if (process.env.FORGEYARD_DIR) {
  // Le vrai analyseur de Forgeyard, avec un test temporaire ajouté à son paquet puis retiré.
  const dir = resolve(process.env.FORGEYARD_DIR.replace(/^~/, process.env.HOME));
  const test = join(dir, "internal/compose/zz_shameless_compat_test.go");
  writeFileSync(test, `package compose
import ("os";"testing")
func TestShamelessCompat(t *testing.T){
 b,_:=os.ReadFile(os.Getenv("SHAMELESS_COMPOSE"))
 _,errs:=Parse(b,map[string]string{})
 for _,e:=range errs{ t.Errorf("ligne %d : %s", e.Line, e.Message) }
}
`);
  try {
    const g = spawnSync("go", ["test", "./internal/compose", "-run", "TestShamelessCompat", "-count=1"], {
      cwd: dir,
      encoding: "utf8",
      env: { ...process.env, SHAMELESS_COMPOSE: join(racine, "compose.yaml") },
    });
    assert.equal(g.status, 0, `Forgeyard refuse compose.yaml :\n${g.stdout}${g.stderr}`);
    console.log("ok   l'analyseur de Forgeyard accepte compose.yaml tel quel");
  } finally {
    rmSync(test, { force: true });
  }
}
console.log(`Compatibilité Forgeyard OK (${ok} vérifications)`);
