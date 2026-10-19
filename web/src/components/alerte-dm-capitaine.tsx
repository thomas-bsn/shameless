import type { Schemas } from "@/api/client";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";

type Liens = Schemas["LiensSerieResponse"];

const raisons: Record<string, string> = {
  compte_non_lie: "Compte Discord non lié à ce joueur",
  dm_ferme: "DM fermé",
  autre: "Le bot n'a pas pu écrire au joueur",
};

/**
 * Le bot n'a pas pu envoyer en DM le Lien capitaine de l'Équipe (ADR 0017) : l'organisateur le copie
 * et le transmet à la main. Rien ne s'affiche sans bot, ou quand le DM est parti.
 */
export function AlerteDmCapitaine({ equipe, liens }: { equipe: "A" | "B"; liens: Liens }) {
  const echec = equipe === "A" ? liens.echecDmA : liens.echecDmB;
  if (!echec) return null;

  return (
    <Alert variant="destructive">
      <AlertTitle>Le DM du Lien capitaine de l'Équipe {equipe} n'est pas parti</AlertTitle>
      <AlertDescription>
        {raisons[echec.raison] ?? raisons.autre}. Copiez le lien ci-dessus et envoyez-le au Capitaine.
      </AlertDescription>
    </Alert>
  );
}
