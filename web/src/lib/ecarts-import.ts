export interface GroupeDEcarts {
  titre: string;
  /** Les joueurs concernés, ou rien pour un écart qui ne concerne pas un joueur (le vainqueur). */
  noms: string[];
  nombre: number;
}

const GROUPES: { titre: string; motif: RegExp; nom?: (message: string) => string | undefined }[] = [
  { titre: "Participants absents du fichier", motif: /est participant de la game mais absent du fichier/, nom: (m) => m.split(" est participant")[0] },
  { titre: "Joueurs du fichier absents de la game", motif: /^Le joueur .* est dans le fichier/, nom: (m) => /^Le joueur (.*) est dans le fichier/.exec(m)?.[1] },
  { titre: "Sides différents", motif: / a joué le Side /, nom: (m) => m.split(" a joué le Side")[0] },
  { titre: "Vainqueur différent", motif: /^Le vainqueur diffère/ },
];

/** Regroupe les écarts d'un import refusé : participants absents, joueurs inconnus, Sides, vainqueur. Les groupes vides restent, à zéro. */
export function grouperLesEcarts(ecarts: string[]): { groupes: GroupeDEcarts[]; autres: string[] } {
  const restants = new Set(ecarts);
  const groupes = GROUPES.map(({ titre, motif, nom }) => {
    const messages = ecarts.filter((m) => motif.test(m));
    for (const m of messages) restants.delete(m);
    return { titre, noms: nom ? messages.map(nom).filter((n): n is string => !!n) : [], nombre: messages.length };
  });
  return { groupes, autres: [...restants] };
}
