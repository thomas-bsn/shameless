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
          <div className="mt-1 text-sm text-muted-foreground">{texte}</div>
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
  ["Spectateur", "Un membre du serveur Discord qui regarde sans jouer : il fait des pronostics et vote pour son Roi/Reine."],
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
    id: "lancer-une-game",
    titre: "Lancer une game",
    corps: (
      <TroisBlocs
        aQuoiCaSert="Passer des équipes formées à une game en cours : les Capitaines draftent les champions, puis la game démarre toute seule."
        pourquoi="Les Capitaines mènent leur Équipe sans passer par l'organisateur, et personne n'a de bouton à presser pour lancer la partie."
        comment="L'organisateur forme les équipes et désigne un Capitaine par Équipe dans le Picker (étape « Capitaines »). Chaque Capitaine reçoit son lien, l'ouvre, puis se déclare « prêt » : quand les deux le sont, la Draft de champions démarre. Pendant la draft, un Capitaine peut demander un Reset (l'autre doit accepter sous 60 s) ou demander une pause (l'autre doit l'accepter sous 60 s ; une seule par Capitaine, 5 minutes au plus ; reprendre avant demande aussi son accord). Au dernier pick, la game démarre automatiquement. « Prêt » vaut pour chaque game de la Série : après une game, les Capitaines se redéclarent prêts pour la suivante, et l'Espace organisateur n'a rien à lancer."
      />
    ),
  },
  {
    id: "companion",
    titre: "Companion",
    corps: (
      <TroisBlocs
        aQuoiCaSert="Le Companion est une petite app PC qui lit les stats de fin de game dans le client LoL et les envoie à Shameless. Il ne sert que pour les customs 5v5, et seulement sous Windows 10 ou 11."
        pourquoi="Les stats sont celles du jeu, sans saisie à la main, et les classements restent justes. Un seul joueur équipé par game suffit."
        comment={
          <ol className="flex list-decimal flex-col gap-2 pl-5">
            <li>
              Télécharge le Companion (Windows 10 ou 11 seulement) :{" "}
              <a className="font-medium text-foreground underline" href="https://github.com/thomas-bsn/shameless/releases/latest/download/shameless-companion.exe">
                Télécharger le Companion
              </a>
              , puis lance le fichier shameless-companion.exe. Le même bouton est dans l'Écran de préparation de la Série.
            </li>
            <li>
              Windows peut afficher « Windows a protégé votre ordinateur » avec un éditeur inconnu : le fichier n'est pas signé. Clique sur
              Informations complémentaires, puis sur Exécuter quand même.
            </li>
            <li>
              Saisis l'adresse de l'Instance (l'adresse du site) et le Code Companion de la Série : tous deux sont à copier dans l'Écran de
              préparation.
            </li>
            <li>
              Quand tout est prêt, l'app affiche client LoL détecté et Série reconnue. Lance ensuite la game normalement : le rapport part tout
              seul à la fin de la partie.
            </li>
            <li>
              Si l'envoi échoue, l'app propose d'ouvrir un Fichier de Rapport. Donne-le à un Superadministrateur ou Administrateur (l'app affiche
              leurs pseudos) : il l'importe sur le site à ta place.
            </li>
          </ol>
        }
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
