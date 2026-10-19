import type { ReactNode } from "react";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

/**
 * Un sujet du Tutoriel. Pour en ajouter un, ajouter une entrée à `sujets` (en bas de ce fichier) :
 * la page, l'étiquette « Bientôt » et la mise en page sont déjà prises en charge.
 */
export interface Sujet {
  id: string;
  titre: string;
  /** Affiche l'étiquette « Bientôt » : la fonctionnalité n'existe pas encore. */
  bientot?: boolean;
  corps: ReactNode;
}

/** Les trois blocs d'un sujet : à quoi ça sert, pourquoi, comment ça marche. */
export function TroisBlocs({ aQuoiCaSert, pourquoi, comment }: { aQuoiCaSert: ReactNode; pourquoi: ReactNode; comment: ReactNode }) {
  const blocs = [
    ["À quoi ça sert", aQuoiCaSert],
    ["Pourquoi", pourquoi],
    ["Comment ça marche", comment],
  ] as const;
  return (
    <div className="flex flex-col gap-4">
      {blocs.map(([titre, texte]) => (
        <div key={titre}>
          <h3 className="text-sm font-semibold">{titre}</h3>
          <p className="mt-1 text-sm text-muted-foreground">{texte}</p>
        </div>
      ))}
    </div>
  );
}

export function SujetDuTutoriel({ sujet }: { sujet: Sujet }) {
  const titreId = `tutoriel-${sujet.id}`;
  return (
    <section aria-labelledby={titreId} id={sujet.id}>
      <Card>
        <CardHeader>
          <CardTitle id={titreId} className="flex flex-wrap items-center gap-2 text-lg">
            {sujet.titre}
            {sujet.bientot && <Badge variant="secondary">Bientôt</Badge>}
          </CardTitle>
        </CardHeader>
        <CardContent>{sujet.corps}</CardContent>
      </Card>
    </section>
  );
}

const roles: [string, string][] = [
  ["Joueur", "Une personne qui joue les customs, identifiée par son compte Riot. Elle peut lier son compte Discord pour retrouver ses stats."],
  ["Capitaine", "Le joueur qui mène une Équipe dans la Draft et dans la Draft de champions, avec son Lien capitaine."],
  ["Organisateur", "Celui qui ouvre la Soirée, crée la Série et la pilote jusqu'au bout."],
  ["Administrateur", "Un Utilisateur qui corrige les games, fixe les costs et les peaks, et gère les clips."],
  ["Spectateur", "Un membre du serveur Discord qui regarde sans jouer : il fait des pronostics et vote pour son MVP."],
];

/** Les sujets, dans l'ordre d'affichage. */
export const sujets: Sujet[] = [
  {
    id: "qui-est-quoi",
    titre: "Qui est quoi",
    corps: (
      <dl className="flex flex-col gap-3 text-sm">
        {roles.map(([role, definition]) => (
          <div key={role}>
            <dt className="font-semibold">{role}</dt>
            <dd className="text-muted-foreground">{definition}</dd>
          </div>
        ))}
      </dl>
    ),
  },
  {
    id: "companion",
    titre: "Companion",
    bientot: true,
    corps: (
      <TroisBlocs
        aQuoiCaSert="Le Companion est une petite app PC qui lit les stats de fin de game dans le client LoL et les envoie à Shameless."
        pourquoi="Les stats sont celles du jeu, sans saisie à la main, et les classements restent justes."
        comment="Un seul joueur équipé par game suffit. Il saisit le Code Companion de la Série dans l'app, qui envoie le rapport à la fin de la partie."
      />
    ),
  },
  {
    id: "lol-proxchat",
    titre: "LoL Proximity Chat",
    bientot: true,
    corps: (
      <TroisBlocs
        aQuoiCaSert="LoL Proximity Chat (« LoL Proxchat ») fait parler les joueurs en vocal comme en vrai : on entend ceux qui sont près de soi dans la partie."
        pourquoi="Pour rendre les customs plus vivants : les rencontres en jeu deviennent des conversations."
        comment="Chaque joueur lance l'outil sur son PC pendant la game : le volume des voix suit la distance entre les champions."
      />
    ),
  },
];
