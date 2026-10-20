import { readFile, readdir } from "node:fs/promises";
import { extname, join } from "node:path";
import type { DossierDeCollecte, Game, LigneDeScore, Participant } from "./types.ts";

/** Lit un dossier produit par `tools/riot-collecte` : `index.json` (facultatif ici) et `matches/*.json`. Ne lit rien d'autre. */
export async function lireDossier(dossier: string): Promise<DossierDeCollecte> {
  let cible: DossierDeCollecte["cible"];
  try {
    const index = JSON.parse(await readFile(join(dossier, "index.json"), "utf8"));
    if (index?.cible?.riotId) cible = { riotId: String(index.cible.riotId), puuid: String(index.cible.puuid ?? "") };
  } catch (e) {
    if ((e as NodeJS.ErrnoException).code !== "ENOENT") throw new Error(`index.json illisible dans ${dossier} : ${(e as Error).message}`);
  }

  const fichiers = (await readdir(join(dossier, "matches"))).filter((f) => f.endsWith(".json")).sort();
  const games: Game[] = [];
  for (const f of fichiers) {
    const doc = JSON.parse(await readFile(join(dossier, "matches", f), "utf8"));
    const info = doc?.info;
    if (!info || !Array.isArray(info.participants)) throw new Error(`matches/${f} : pas un document match-v5`);
    games.push({
      matchId: String(doc.metadata?.matchId ?? f.replace(/\.json$/, "")),
      debut: Number(info.gameStartTimestamp),
      duree: Number(info.gameDuration),
      participants: info.participants as Participant[],
    });
  }
  return { cible, games };
}

/** Lit un fichier de scores dpm.lol saisi à la main, CSV ou JSON selon l'extension (voir le README). */
export async function lireScores(chemin: string): Promise<LigneDeScore[]> {
  const texte = await readFile(chemin, "utf8");
  return analyserScores(texte, extname(chemin).toLowerCase() === ".json" ? "json" : "csv");
}

const ALIAS: Record<string, string> = {
  k: "kills",
  d: "deaths",
  morts: "deaths",
  a: "assists",
  duree: "duree",
  durée: "duree",
};
const OBLIGATOIRES = ["date", "duree", "champion", "kills", "deaths", "assists", "score"];

export function analyserScores(texte: string, format: "csv" | "json"): LigneDeScore[] {
  const brut = format === "json" ? lignesJson(texte) : lignesCsv(texte);
  return brut.map((champs, i) => versLigne(champs, i + 1));
}

function lignesJson(texte: string): Record<string, unknown>[] {
  const donnees = JSON.parse(texte);
  if (!Array.isArray(donnees)) throw new Error("Le JSON de scores doit être un tableau d'objets, un par game.");
  return donnees.map((o) => normaliserCles(o as Record<string, unknown>));
}

function lignesCsv(texte: string): Record<string, unknown>[] {
  const lignes = texte.replace(/^﻿/, "").split(/\r?\n/).filter((l) => l.trim() !== "");
  if (lignes.length === 0) throw new Error("Fichier de scores vide.");
  const sep = lignes[0]!.includes(";") ? ";" : ",";
  const entetes = lignes[0]!.split(sep).map((h) => h.trim());
  manquantes(entetes.map(cle));
  return lignes.slice(1).map((l) => {
    const valeurs = l.split(sep);
    return normaliserCles(Object.fromEntries(entetes.map((h, i) => [h, (valeurs[i] ?? "").trim()])));
  });
}

const cle = (h: string) => ALIAS[h.trim().toLowerCase()] ?? h.trim().toLowerCase();

function normaliserCles(o: Record<string, unknown>): Record<string, unknown> {
  return Object.fromEntries(Object.entries(o).map(([k, v]) => [cle(k), v]));
}

function manquantes(entetes: string[]) {
  const absentes = OBLIGATOIRES.filter((c) => !entetes.includes(c));
  if (absentes.length > 0) throw new Error(`Colonne(s) manquante(s) dans le fichier de scores : ${absentes.join(", ")}.`);
}

function versLigne(c: Record<string, unknown>, numero: number): LigneDeScore {
  const erreur = (champ: string, detail: string) => new Error(`Fichier de scores, ligne ${numero} : ${champ} invalide (${detail}).`);
  manquantes(Object.keys(c));
  const date = lireDate(c.date);
  if (date === null) throw erreur("date", String(c.date));
  const duree = lireDuree(c.duree);
  if (duree === null) throw erreur("duree", String(c.duree));
  const champion = String(c.champion ?? "").trim();
  if (champion === "") throw erreur("champion", "vide");
  const nombre = (champ: string, v: unknown) => {
    const n = lireNombre(v);
    if (n === null) throw erreur(champ, String(v));
    return n;
  };
  const ligne: LigneDeScore = {
    numero,
    date,
    duree,
    champion,
    kills: nombre("kills", c.kills),
    deaths: nombre("deaths", c.deaths),
    assists: nombre("assists", c.assists),
    score: nombre("score", c.score),
  };
  if (c.place !== undefined && c.place !== null && String(c.place).trim() !== "") ligne.place = nombre("place", c.place);
  return ligne;
}

function lireNombre(v: unknown): number | null {
  if (typeof v === "number") return Number.isFinite(v) ? v : null;
  const s = String(v ?? "").trim().replace(",", ".");
  if (s === "") return null;
  const n = Number(s);
  return Number.isFinite(n) ? n : null;
}

/** ISO 8601 ; sans fuseau, la date est en UTC. */
function lireDate(v: unknown): number | null {
  if (typeof v === "number") return v;
  let s = String(v ?? "").trim();
  if (/^\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}(:\d{2})?$/.test(s)) s = s.replace(" ", "T") + "Z";
  else if (/^\d{4}-\d{2}-\d{2}$/.test(s)) s += "T00:00:00Z";
  const t = Date.parse(s);
  return Number.isNaN(t) ? null : t;
}

/** Secondes (`1812`) ou `mm:ss` (`30:12`). */
function lireDuree(v: unknown): number | null {
  const m = /^(\d+):(\d{2})$/.exec(String(v ?? "").trim());
  return m ? Number(m[1]) * 60 + Number(m[2]) : lireNombre(v);
}
