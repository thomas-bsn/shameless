import type { Schemas } from "@/api/client";

type Tour = Schemas["TourDraftResponse"];

/**
 * Le libellé du tour <numero> de l'ordre Tournament donné par l'API, ex. « Pick red 2 » : son rang
 * parmi les tours de même action et même Side.
 */
export function libelleTour(tours: Tour[], numero: number): string {
  const tour = tours.find((t) => t.numero === numero);
  if (!tour) return `Tour ${numero}`;
  const rang = tours.filter((t) => t.numero <= numero && t.action === tour.action && t.side === tour.side).length;
  return `${tour.action === "ban" ? "Ban" : "Pick"} ${tour.side} ${rang}`;
}

type Draft = Pick<Schemas["DraftChampionsResponse"], "terminee" | "tourCourant" | "tours" | "actions" | "pause">;

/** Un tour de la Draft de champions d'une game : l'Équipe qui le joue, son libellé, son champion une fois joué. */
export interface TourDeLaDraft extends Tour {
  libelle: string;
  championId: string | null;
  courant: boolean;
}

/** Les vingt tours de la Draft dans l'ordre Tournament donné par l'API, joués ou à venir. */
export function toursDeLaDraft(draft: Draft): TourDeLaDraft[] {
  const joues = new Map(draft.actions.map((a) => [a.numero, a.championId]));
  return draft.tours.map((tour) => ({
    ...tour,
    libelle: libelleTour(draft.tours, tour.numero),
    championId: joues.get(tour.numero) ?? null,
    courant: draft.tourCourant?.numero === tour.numero,
  }));
}

/** Le capitaine de cette Équipe peut jouer : la Draft est en cours et le tour courant revient à son Équipe. */
export function peutJouer(draft: Draft, equipe: string | null): boolean {
  return !draft.terminee && !draft.pause && equipe !== null && draft.tourCourant?.equipe === equipe;
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

/** Les voies du filtre, dans l'ordre du client LoL, avec les valeurs de l'API. */
export const VOIES = [
  { id: "top", libelle: "Top" },
  { id: "jungle", libelle: "Jungle" },
  { id: "mid", libelle: "Mid" },
  { id: "adc", libelle: "ADC" },
  { id: "support", libelle: "Support" },
] as const;

export type Voie = (typeof VOIES)[number]["id"];

/**
 * Les champions dont le nom ou l'identifiant contient la recherche (casse, accents et ponctuation ignorés),
 * par nom. Avec une `voie`, seulement ceux qui s'y jouent ; un champion sans donnée de voie reste visible partout.
 */
export function filtrerChampions<C extends Pick<Schemas["ChampionResponse"], "id" | "name" | "voies">>(
  champions: C[],
  recherche: string,
  voie: Voie | null = null,
): C[] {
  const cherche = normaliser(recherche);
  return champions
    .filter((c) => normaliser(c.name).includes(cherche) || normaliser(c.id).includes(cherche))
    .filter((c) => voie === null || c.voies.length === 0 || c.voies.includes(voie))
    .sort((a, b) => a.name.localeCompare(b.name, "fr"));
}

/** Les secondes qui restent pour jouer le tour courant, jusqu'à son échéance donnée par l'API. */
export function secondesRestantes(
  tour: Pick<Schemas["TourCourantResponse"], "echeance">,
  maintenant: number = Date.now(),
): number {
  return Math.max(0, Math.ceil((Date.parse(tour.echeance) - maintenant) / 1000));
}

/**
 * L'organisateur peut jouer le tour courant à la place de l'Équipe attendue : la Draft est en cours
 * et l'échéance du tour est atteinte (avant, l'API refuse en 409).
 */
export function mainALOrganisateur(
  draft: Pick<Draft, "terminee" | "tourCourant" | "pause">,
  maintenant: number = Date.now(),
): boolean {
  return (
    !draft.terminee &&
    !draft.pause &&
    draft.tourCourant !== null &&
    Date.parse(draft.tourCourant.echeance) <= maintenant
  );
}

/** Un décompte en minutes et secondes, ex. « 4:05 ». */
export function formaterDecompte(secondes: number): string {
  const s = Math.max(0, secondes);
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`;
}
