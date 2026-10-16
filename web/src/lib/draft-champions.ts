import type { Schemas } from "@/api/client";

export type ActionDraft ="ban" | "pick";
export type Side = "blue" | "red";

/** Un tour de l'ordre Tournament : son numéro (1 à 20), l'action et le Side qui la joue. */
export interface TourTournament {
  numero: number;
  action: ActionDraft;
  side: Side;
}

// bans B1 R1 B2 R2 B3 R3, picks B1 R1 R2 B2 B3 R3, bans R4 B4 R5 B5, picks R4 B4 B5 R5 (comme l'API)
const sequence: [ActionDraft, Side][] = [
  ["ban", "blue"], ["ban", "red"], ["ban", "blue"], ["ban", "red"], ["ban", "blue"], ["ban", "red"],
  ["pick", "blue"], ["pick", "red"], ["pick", "red"], ["pick", "blue"], ["pick", "blue"], ["pick", "red"],
  ["ban", "red"], ["ban", "blue"], ["ban", "red"], ["ban", "blue"],
  ["pick", "red"], ["pick", "blue"], ["pick", "blue"], ["pick", "red"],
];

/** L'ordre fixe du mode Tournament : vingt tours. */
export const ordreTournament: TourTournament[] = sequence.map(([action, side], i) => ({ numero: i + 1, action, side }));

/** Le libellé d'un tour, ex. « Pick red 2 » : son rang parmi les tours de même action et même Side. */
export function libelleTour(numero: number): string {
  const tour = ordreTournament[numero - 1];
  const rang = ordreTournament
    .slice(0, numero)
    .filter((t) => t.action === tour.action && t.side === tour.side).length;
  return `${tour.action === "ban" ? "Ban" : "Pick"} ${tour.side} ${rang}`;
}

type Draft = Pick<Schemas["DraftChampionsResponse"], "equipeBlue" | "terminee" | "tourCourant" | "actions">;

/** Un tour de la Draft de champions d'une game : l'Équipe qui le joue, son champion une fois joué. */
export interface TourDeLaDraft extends TourTournament {
  equipe: string;
  championId: string | null;
  courant: boolean;
}

const autreEquipe = (equipe: string) => (equipe === "A" ? "B" : "A");

/** Les vingt tours de la Draft dans l'ordre Tournament, joués ou à venir. */
export function toursDeLaDraft(draft: Draft): TourDeLaDraft[] {
  const joues = new Map(draft.actions.map((a) => [a.numero, a.championId]));
  return ordreTournament.map((tour) => ({
    ...tour,
    equipe: tour.side === "blue" ? draft.equipeBlue : autreEquipe(draft.equipeBlue),
    championId: joues.get(tour.numero) ?? null,
    courant: draft.tourCourant?.numero === tour.numero,
  }));
}

/** Le capitaine de cette Équipe peut jouer : la Draft est en cours et le tour courant revient à son Équipe. */
export function peutJouer(draft: Draft, equipe: string | null): boolean {
  return !draft.terminee && equipe !== null && draft.tourCourant?.equipe === equipe;
}

/** Les champions déjà bannis ou pick dans la game : plus disponibles pour la Draft de champions. */
export function championsPris(actions: Pick<Schemas["ActionDraftResponse"], "championId">[]): Set<string> {
  return new Set(actions.map((a) => a.championId));
}

const normaliser = (texte: string) =>
  texte
    .normalize("NFD")
    .replace(/[^a-zA-Z0-9]/g, "")
    .toLowerCase();

/** Les champions dont le nom ou l'identifiant contient la recherche (casse, accents et ponctuation ignorés), par nom. */
export function filtrerChampions<C extends Pick<Schemas["ChampionResponse"], "id" | "name">>(
  champions: C[],
  recherche: string,
): C[] {
  const cherche = normaliser(recherche);
  return champions
    .filter((c) => normaliser(c.name).includes(cherche) || normaliser(c.id).includes(cherche))
    .sort((a, b) => a.name.localeCompare(b.name, "fr"));
}

/** Une date de l'API ; sans fuseau (timestamp sans fuseau en base), c'est de l'UTC. */
function instantUtc(iso: string): number {
  return Date.parse(/(Z|[+-]\d{2}:?\d{2})$/i.test(iso) ? iso : `${iso}Z`);
}

/** Les secondes qui restent pour jouer le tour courant, jusqu'à son échéance donnée par l'API. */
export function secondesRestantes(
  tour: Pick<Schemas["TourCourantResponse"], "echeance">,
  maintenant: number = Date.now(),
): number {
  return Math.max(0, Math.ceil((instantUtc(tour.echeance) - maintenant) / 1000));
}
