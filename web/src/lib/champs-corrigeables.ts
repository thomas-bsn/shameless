import type { Schemas } from "@/api/client";

type Participant = Schemas["GameHistoryParticipantDto"];
type Stats = Schemas["StatsDuParticipant"];
type Corps = Schemas["CorrectPlayerStatsRequest"];

/** Ce que contient une case : un nombre, un texte, ou un identifiant du catalogue de jeu. */
export type TypeDeChamp = "nombre" | "champion" | "voie" | "objet" | "sort" | "rune" | "arbre";

export interface ChampCorrigeable {
  /** Le nom du champ dans l'API (camelCase), aussi la clé de la correction. */
  nom: keyof Corps;
  label: string;
  type: TypeDeChamp;
  section: string;
}

const nombre = (nom: keyof Corps, label: string, section: string): ChampCorrigeable => ({ nom, label, type: "nombre", section });

const objets = (): ChampCorrigeable[] => [
  ...[0, 1, 2, 3, 4, 5].map((i) => ({ nom: `item${i}` as keyof Corps, label: `Objet ${i + 1}`, type: "objet" as const, section: "Objets" })),
  { nom: "item6", label: "Babiole", type: "objet", section: "Objets" },
  { nom: "roleBoundItem", label: "Objet de rôle", type: "objet", section: "Objets" },
];

const runes = (): ChampCorrigeable[] => [
  { nom: "perkPrimaryStyle", label: "Arbre principal", type: "arbre", section: "Runes" },
  { nom: "perkSubStyle", label: "Arbre secondaire", type: "arbre", section: "Runes" },
  ...[0, 1, 2, 3, 4, 5].map((i) => ({ nom: `perk${i}` as keyof Corps, label: `Rune ${i + 1}`, type: "rune" as const, section: "Runes" })),
];

/** Les champs qu'un Administrateur peut corriger, rangés par section dans l'ordre de la fenêtre. Pas de fragments de runes. */
export const CHAMPS: ChampCorrigeable[] = [
  { nom: "champion", label: "Champion", type: "champion", section: "Identité" },
  { nom: "position", label: "Voie", type: "voie", section: "Identité" },
  nombre("champLevel", "Niveau", "Identité"),
  { nom: "spell1Id", label: "Sort 1", type: "sort", section: "Identité" },
  { nom: "spell2Id", label: "Sort 2", type: "sort", section: "Identité" },
  nombre("kills", "Kills", "Combat"),
  nombre("deaths", "Morts", "Combat"),
  nombre("assists", "Assistances", "Combat"),
  nombre("largestKillingSpree", "Plus longue série", "Combat"),
  nombre("killingSprees", "Séries de kills", "Combat"),
  nombre("doubleKills", "Doubles kills", "Combat"),
  nombre("tripleKills", "Triples kills", "Combat"),
  nombre("quadraKills", "Quadruples kills", "Combat"),
  nombre("pentaKills", "Pentakills", "Combat"),
  nombre("minionsKilled", "Sbires", "Farm et or"),
  nombre("neutralMinionsKilled", "Monstres neutres", "Farm et or"),
  nombre("goldEarned", "Or gagné", "Farm et or"),
  nombre("goldSpent", "Or dépensé", "Farm et or"),
  nombre("damageToChampions", "Dégâts aux champions", "Dégâts"),
  nombre("physicalDamageToChampions", "Dégâts physiques", "Dégâts"),
  nombre("magicDamageToChampions", "Dégâts magiques", "Dégâts"),
  nombre("trueDamageToChampions", "Dégâts vrais", "Dégâts"),
  nombre("damageTaken", "Dégâts subis", "Dégâts"),
  nombre("totalHeal", "Soins", "Dégâts"),
  nombre("damageShielded", "Boucliers alliés", "Dégâts"),
  nombre("timeCcingOthers", "Contrôle infligé (s)", "Dégâts"),
  nombre("visionScore", "Score de vision", "Vision"),
  nombre("wardsPlaced", "Balises posées", "Vision"),
  nombre("wardsKilled", "Balises détruites", "Vision"),
  nombre("controlWardsBought", "Balises de contrôle", "Vision"),
  nombre("damageToObjectives", "Dégâts aux objectifs", "Objectifs"),
  nombre("turretKills", "Tourelles", "Objectifs"),
  nombre("dragonKills", "Dragons", "Objectifs"),
  nombre("baronKills", "Barons", "Objectifs"),
  nombre("inhibitorKills", "Inhibiteurs", "Objectifs"),
  ...objets(),
  ...runes(),
  nombre("note", "Note", "Note"),
];

export const SECTIONS = [...new Set(CHAMPS.map((c) => c.section))];

export const VOIES = ["Top", "Jungle", "Mid", "Adc", "Support"];

const texte = (v: string | number | null | undefined): string => (v == null ? "" : String(v));

const lire = (stats: Stats | null, nom: string): string => {
  if (!stats) return "";
  const index = /^(item|perk)(\d)$/.exec(nom);
  if (index) return texte((index[1] === "item" ? stats.items : stats.perks)?.[Number(index[2])]);
  return texte((stats as unknown as Record<string, string | number | null>)[nom]);
};

/** La valeur affichée d'un champ (corrigée s'il y a une correction), en texte de formulaire ; vide si inconnue. */
export function valeurCourante(p: Participant, nom: string): string {
  if (nom === "champion") return texte(p.championId);
  if (nom === "note") return texte(p.performance?.note);
  return lire(p.stats, nom);
}

/** La valeur du Companion, avant toute correction ; null quand le Companion n'a rien envoyé pour ce joueur. */
export function valeurOrigine(p: Participant, nom: string): string | null {
  if (nom === "champion") return p.championOrigine ?? null;
  if (nom === "note") return p.performance?.noteOrigine == null ? null : String(p.performance.noteOrigine);
  return p.statsOrigine ? lire(p.statsOrigine, nom) : null;
}

/** Les champs changés (valeur saisie différente de la valeur affichée), prêts à envoyer. */
export function corpsDeCorrection(p: Participant, brouillon: Record<string, string>): Corps | null {
  const corps = Object.fromEntries(Object.keys(CORPS_VIDE).map((k) => [k, null])) as Record<string, string | number | null>;
  let change = false;
  for (const champ of CHAMPS) {
    const saisie = brouillon[champ.nom]?.trim();
    if (saisie == null || saisie === "" || saisie === valeurCourante(p, champ.nom)) continue;
    corps[champ.nom] = champ.type === "champion" || champ.type === "voie" ? saisie : Number(saisie);
    change = true;
  }
  return change ? (corps as unknown as Corps) : null;
}

const CORPS_VIDE: Record<keyof Corps, null> = {
  champion: null, position: null, kills: null, deaths: null, assists: null, cs: null, goldEarned: null,
  damageToChampions: null, visionScore: null, note: null, champLevel: null, spell1Id: null, spell2Id: null,
  largestKillingSpree: null, killingSprees: null, doubleKills: null, tripleKills: null, quadraKills: null,
  pentaKills: null, minionsKilled: null, neutralMinionsKilled: null, goldSpent: null,
  physicalDamageToChampions: null, magicDamageToChampions: null, trueDamageToChampions: null, damageTaken: null,
  totalHeal: null, damageShielded: null, timeCcingOthers: null, wardsPlaced: null, wardsKilled: null,
  controlWardsBought: null, damageToObjectives: null, turretKills: null, dragonKills: null, baronKills: null,
  inhibitorKills: null, roleBoundItem: null, perkPrimaryStyle: null, perkSubStyle: null,
  item0: null, item1: null, item2: null, item3: null, item4: null, item5: null, item6: null,
  perk0: null, perk1: null, perk2: null, perk3: null, perk4: null, perk5: null,
};

/** Sans accent ni majuscule, pour la recherche d'une stat. */
export const normaliser = (s: string): string => s.normalize("NFD").replace(/\p{Diacritic}/gu, "").toLowerCase();
