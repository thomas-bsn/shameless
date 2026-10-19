import { useCallback, useEffect, useRef, useState } from "react";
import type { Schemas } from "@/api/client";

type Draft = Schemas["DraftChampionsResponse"];

/** Les moments sonores de la Draft de champions. */
export type Son = "debut" | "tour" | "verrouille" | "fin";

/** Le lecteur audio : remplaçable dans les tests. `disponible` est faux quand aucun son n'est configuré. */
export interface LecteurDeSons {
  disponible: boolean;
  jouer: (son: Son) => void;
}

/**
 * Les sons de LoL sont des fichiers de Riot sous copyright : ils ne sont pas dans le dépôt (licence MIT).
 * Ils sont servis depuis `VITE_DRAFT_SONS_URL` (au build) : `<url>/debut.ogg`, `tour.ogg`, `verrouille.ogg`
 * et `fin.ogg`. Sans cette variable, la draft reste silencieuse.
 */
export function creerLecteurAudio(baseUrl: string | undefined): LecteurDeSons {
  const base = baseUrl?.replace(/\/+$/, "");
  if (!base) return { disponible: false, jouer: () => {} };
  return {
    disponible: true,
    jouer: (son) => {
      try {
        void new Audio(`${base}/${son}.ogg`).play().catch(() => {});
      } catch {
        // Fichier absent ou lecture refusée : la draft continue en silence.
      }
    },
  };
}

let lecteur: LecteurDeSons | null = null;

/** Remplace le lecteur (tests) ; null revient au lecteur configuré par l'environnement. */
export function definirLecteurDeSons(nouveau: LecteurDeSons | null) {
  lecteur = nouveau;
}

function lecteurCourant(): LecteurDeSons {
  lecteur ??= creerLecteurAudio(import.meta.env.VITE_DRAFT_SONS_URL);
  return lecteur;
}

/**
 * Les sons à jouer quand la draft passe de `avant` à `apres`. `avant` vaut undefined avant la première
 * lecture (arrivée en cours de draft : silence) et null tant que la draft n'est pas ouverte (son de début).
 * « À ton tour » ne concerne que le Capitaine de l'Équipe dont c'est le tour.
 */
export function sonsDeLaTransition(avant: Draft | null | undefined, apres: Draft | null, equipe: string | null): Son[] {
  if (avant === undefined || apres === null) return [];
  const sons: Son[] = [];
  if (avant === null) sons.push("debut");
  else if (apres.actions.length > avant.actions.length) sons.push("verrouille");
  const tour = apres.tourCourant;
  if (tour && equipe !== null && tour.equipe === equipe && tour.numero !== avant?.tourCourant?.numero) sons.push("tour");
  if (avant && !avant.terminee && apres.terminee) sons.push("fin");
  return sons;
}

const CLE_SON_COUPE = "shameless.draft.son-coupe";

const lireCoupe = () => {
  try {
    return localStorage.getItem(CLE_SON_COUPE) === "1";
  } catch {
    return false;
  }
};

/**
 * Joue les sons de la draft. `draft` : undefined tant que non chargée, null si non ouverte. Rien ne se joue
 * avant une première action de l'utilisateur sur la page (règle des navigateurs) ni quand le son est coupé ;
 * le choix de couper est retenu sur l'appareil.
 */
export function useSonsDeLaDraft(draft: Draft | null | undefined, equipe: string | null) {
  const [coupe, setCoupe] = useState(lireCoupe);
  const actif = useRef(false);
  const precedent = useRef<Draft | null | undefined>(undefined);
  const disponible = lecteurCourant().disponible;

  useEffect(() => {
    const activer = () => {
      actif.current = true;
    };
    const evenements = ["pointerdown", "keydown", "touchstart"] as const;
    evenements.forEach((e) => document.addEventListener(e, activer, true));
    return () => evenements.forEach((e) => document.removeEventListener(e, activer, true));
  }, []);

  useEffect(() => {
    if (draft === undefined) return;
    const sons = sonsDeLaTransition(precedent.current, draft, equipe);
    precedent.current = draft;
    if (!actif.current || coupe) return;
    sons.forEach((son) => lecteurCourant().jouer(son));
  }, [draft, equipe, coupe]);

  const basculer = useCallback(() => {
    setCoupe((valeur) => {
      try {
        localStorage.setItem(CLE_SON_COUPE, valeur ? "0" : "1");
      } catch {
        // Stockage indisponible : le choix vaut pour cette page seulement.
      }
      return !valeur;
    });
  }, []);

  return { disponible, coupe, basculer };
}
