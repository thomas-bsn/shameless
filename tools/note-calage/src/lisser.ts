/**
 * Lisse les paramètres ajustés par `main.ts` avant de les brancher dans l'API (ticket #182) : un
 * ajustement libre rapproche parfois faible, moyen et excellent jusqu'à un palier (ex. dégâts/min du
 * Top : 0 / 824 / 830), ce qui rend la note instable.
 *
 *   node src/lisser.ts ajuste.json lisse.json [--ecart 0.15] [--poids-min 1] [--ecart-role JUNGLE=0.12]
 *
 * Règles, par rôle (hors INCONNU) :
 * - Faible <= Moyen x (1 - e) et Excellent >= Moyen x (1 + e) : un écart minimal de e entre références ;
 * - chaque critère et la victoire gardent un poids d'au moins `--poids-min` (les poids sont ensuite
 *   arrondis à l'entier, ramenés à une somme de 100), sauf le CS du support, jamais noté ;
 * - références arrondies à 3 décimales (< 1), 2 (< 20) ou à l'unité.
 *
 * Le fichier écrit a le format de `--sortie` de main.ts (clé `parametres`) : il se mesure avec
 * `main.ts --parametres lisse.json --iterations 0`.
 */
import { readFile, writeFile } from "node:fs/promises";
import { parseArgs } from "node:util";
import type { Parametres, Role } from "./note.ts";

const { values, positionals } = parseArgs({
  allowPositionals: true,
  options: { ecart: { type: "string", default: "0.15" }, "poids-min": { type: "string", default: "1" }, "ecart-role": { type: "string", multiple: true } },
});
const [entree, sortie] = positionals;
if (!entree || !sortie) throw new Error("usage : node src/lisser.ts ajuste.json lisse.json [--ecart 0.15] [--poids-min 1] [--ecart-role ROLE=0.12]");

const ecartParRole = new Map((values["ecart-role"] ?? []).map((s) => s.split("=") as [string, string]).map(([r, e]) => [r, Number(e)]));
const poidsMin = Number(values["poids-min"]);

const arrondi = (x: number) => (x === 0 ? 0 : x < 1 ? Math.round(x * 1000) / 1000 : x < 20 ? Math.round(x * 100) / 100 : Math.round(x));

const fichier = JSON.parse(await readFile(entree, "utf8")) as { parametres: Parametres };
for (const role of ["TOP", "JUNGLE", "MID", "ADC", "SUPPORT"] as Role[]) {
  const pr = fichier.parametres.roles[role];
  const e = ecartParRole.get(role) ?? Number(values.ecart);
  const poids = Object.fromEntries(
    Object.entries(pr.criteres).map(([c, r]) => [c, role === "SUPPORT" && c === "csParMinute" ? 0 : Math.max(r.poids, poidsMin)]),
  );
  const victoire = Math.max(pr.poidsVictoire, poidsMin);
  const total = Object.values(poids).reduce((a, b) => a + b, 0) + victoire;
  for (const [c, r] of Object.entries(pr.criteres)) {
    r.poids = Math.round((poids[c]! * 100) / total);
    r.faible = arrondi(Math.min(r.faible, r.moyen * (1 - e)));
    r.excellent = arrondi(Math.max(r.excellent, r.moyen * (1 + e)));
    r.moyen = arrondi(r.moyen);
  }
  pr.poidsVictoire = Math.round((victoire * 100) / total);
}
await writeFile(sortie, JSON.stringify(fichier, null, 2) + "\n");
