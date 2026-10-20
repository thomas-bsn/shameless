import { readFile, writeFile } from "node:fs/promises";
import { parseArgs } from "node:util";
import { ajuster, OPTIONS_PAR_DEFAUT } from "./ajustement.ts";
import { apparier, TOLERANCES_PAR_DEFAUT } from "./appariement.ts";
import { evaluer, separer, versLignesAEvaluer, type Evaluation } from "./evaluation.ts";
import { lireDossier, lireScores } from "./lecture.ts";
import { parametresParDefaut, type Parametres } from "./note.ts";

const AIDE = `Usage : node src/main.ts --dossier ./soloq --scores ./dpm.csv [options]

  --dossier D        dossier produit par tools/riot-collecte (obligatoire)
  --scores F         scores dpm.lol saisis à la main, .csv ou .json (obligatoire)
  --sortie F         écrit les paramètres ajustés et les mesures dans ce fichier JSON
  --parametres F     part de ces paramètres (sortie d'une exécution précédente) au lieu de ceux de l'API
  --ajuster G        poids | references | tout (défaut : ${OPTIONS_PAR_DEFAUT.ajuster})
  --iterations N     itérations de la descente de gradient (défaut : ${OPTIONS_PAR_DEFAUT.iterations})
  --test X           fraction des games mises de côté pour le test (défaut : 0.25)
  --graine N         graine du tirage apprentissage/test (défaut : 1)
  --tolerance-date S écart de date toléré en secondes (défaut : ${TOLERANCES_PAR_DEFAUT.date})
  --tolerance-duree S écart de durée toléré en secondes (défaut : ${TOLERANCES_PAR_DEFAUT.duree})
`;

function lireNombre(nom: string, v: string | undefined, defaut: number): number {
  if (v === undefined) return defaut;
  const n = Number(v);
  if (!Number.isFinite(n)) throw new Error(`--${nom} : nombre attendu, reçu « ${v} »`);
  return n;
}

const pct = (x: number) => (Number.isNaN(x) ? "n/a" : `${(100 * x).toFixed(0)} %`);
const pts = (x: number) => (Number.isNaN(x) ? "n/a" : `${x.toFixed(1)} pts`);

function resume(titre: string, e: Evaluation): string {
  return `  ${titre.padEnd(22)} ${String(e.lignes).padStart(4)} lignes   écart moyen ${pts(e.ecartMoyen).padStart(9)}   même ordre ${pct(e.frequenceDuMemeOrdre).padStart(5)} (${e.gamesDansLeMemeOrdre}/${e.games} games)   paires concordantes ${pct(e.frequenceDesPairesConcordantes)}`;
}

async function main() {
  const { values } = parseArgs({
    options: {
      dossier: { type: "string" },
      scores: { type: "string" },
      sortie: { type: "string" },
      parametres: { type: "string" },
      ajuster: { type: "string" },
      iterations: { type: "string" },
      test: { type: "string" },
      graine: { type: "string" },
      "tolerance-date": { type: "string" },
      "tolerance-duree": { type: "string" },
      aide: { type: "boolean" },
    },
  });
  if (values.aide || !values.dossier || !values.scores) {
    console.log(AIDE);
    process.exitCode = values.aide ? 0 : 1;
    return;
  }
  const groupe = values.ajuster ?? OPTIONS_PAR_DEFAUT.ajuster;
  if (groupe !== "poids" && groupe !== "references" && groupe !== "tout") throw new Error("--ajuster : poids, references ou tout");

  const { games, cible } = await lireDossier(values.dossier);
  const lignes = await lireScores(values.scores);
  const tolerances = {
    date: lireNombre("tolerance-date", values["tolerance-date"], TOLERANCES_PAR_DEFAUT.date),
    duree: lireNombre("tolerance-duree", values["tolerance-duree"], TOLERANCES_PAR_DEFAUT.duree),
  };
  const rapport = apparier(lignes, games, tolerances);

  console.log(`Dossier : ${games.length} games${cible ? ` (cible ${cible.riotId})` : ""}. Scores : ${lignes.length} lignes.`);
  console.log(`Appariement : ${rapport.appariees.length} appariées, ${rapport.ambigues.length} ambiguës, ${rapport.sansCorrespondance.length} sans correspondance.`);
  for (const a of rapport.ambigues) {
    console.log(`  ligne ${a.ligne.numero} (${a.ligne.champion}) ambiguë : ${a.raison} [${a.candidats.map((c) => c.game.matchId).join(", ")}]`);
  }
  for (const s of rapport.sansCorrespondance) console.log(`  ligne ${s.ligne.numero} (${s.ligne.champion}) sans correspondance : ${s.raison}`);

  const toutes = versLignesAEvaluer(rapport.appariees);
  if (toutes.length === 0) throw new Error("Aucune ligne appariée : rien à ajuster. Vérifier les tolérances et le fichier de scores.");
  const { apprentissage, test } = separer(toutes, lireNombre("test", values.test, 0.25), lireNombre("graine", values.graine, 1));

  const depart: Parametres = values.parametres ? (JSON.parse(await readFile(values.parametres, "utf8")).parametres as Parametres) : parametresParDefaut();
  const { parametres } = ajuster(
    apprentissage.map((l) => l.echantillon),
    depart,
    { ajuster: groupe, iterations: lireNombre("iterations", values.iterations, OPTIONS_PAR_DEFAUT.iterations) },
  );

  const mesures = {
    avantApprentissage: evaluer(apprentissage, depart),
    avantTest: evaluer(test, depart),
    apresApprentissage: evaluer(apprentissage, parametres),
    apresTest: evaluer(test, parametres),
  };
  console.log(`\nSéparation par game : ${new Set(apprentissage.map((l) => l.matchId)).size} games d'apprentissage, ${new Set(test.map((l) => l.matchId)).size} de test.`);
  console.log("Avant ajustement :");
  console.log(resume("apprentissage", mesures.avantApprentissage));
  console.log(resume("test (mises de côté)", mesures.avantTest));
  console.log("Après ajustement :");
  console.log(resume("apprentissage", mesures.apresApprentissage));
  console.log(resume("test (mises de côté)", mesures.apresTest));

  if (values.sortie) {
    await writeFile(values.sortie, JSON.stringify({ parametres, ...mesures }, null, 2) + "\n");
    console.log(`\nParamètres écrits dans ${values.sortie}.`);
  }
}

main().catch((e) => {
  console.error(`Erreur : ${(e as Error).message}`);
  process.exitCode = 1;
});
