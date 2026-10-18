import { createFileRoute } from "@tanstack/react-router";
import { PageHeader } from "@/components/page-header";

export const Route = createFileRoute("/_connecte/administration")({
  component: Administration,
});

/** La page des outils réservés aux Administrateurs : une coquille, ses onglets arrivent avec le ticket #100. */
function Administration() {
  return (
    <>
      <PageHeader title="Administration" description="Les outils réservés aux Administrateurs." />
      <p className="text-muted-foreground">Cette page arrive bientôt.</p>
    </>
  );
}
