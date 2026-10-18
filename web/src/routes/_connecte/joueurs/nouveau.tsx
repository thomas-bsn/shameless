import { createFileRoute, Link } from "@tanstack/react-router";
import { FormulaireNouveauJoueur } from "@/components/formulaire-nouveau-joueur";
import { PageHeader } from "@/components/page-header";

export const Route = createFileRoute("/_connecte/joueurs/nouveau")({ component: NouveauJoueur });

function NouveauJoueur() {
  return <div className="flex max-w-xl flex-col gap-6">
    <Link to="/joueurs" className="text-sm text-muted-foreground hover:text-foreground">← Joueurs</Link>
    <PageHeader title="Ajouter un joueur" description="Le compte Riot est vérifié avant l'enregistrement." />
    <FormulaireNouveauJoueur />
  </div>;
}
