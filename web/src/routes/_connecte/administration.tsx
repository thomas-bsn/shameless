import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { ATraiter } from "@/components/administration/a-traiter";
import { JoueursAdmin } from "@/components/administration/joueurs-admin";
import { PageHeader } from "@/components/page-header";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";

/**
 * Les onglets de la page : pour en ajouter un (ex. les Clips du Fond), l'inscrire ici.
 * Le premier est l'onglet par défaut (pas de paramètre dans l'adresse).
 */
const ONGLETS = [{ id: "joueurs", label: "Joueurs", contenu: <JoueursAdmin /> }];

const onglets: string[] = ONGLETS.map((o) => o.id);
const ONGLET_PAR_DEFAUT = ONGLETS[0].id;

export const Route = createFileRoute("/_connecte/administration")({
  validateSearch: (search: Record<string, unknown>): { onglet?: string } =>
    typeof search.onglet === "string" && search.onglet !== ONGLET_PAR_DEFAUT && onglets.includes(search.onglet)
      ? { onglet: search.onglet }
      : {},
  component: Administration,
});

function Administration() {
  const { user } = Route.useRouteContext();
  if (!user.isAdmin) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Accès refusé</AlertTitle>
        <AlertDescription>L'administration est réservée aux administrateurs de l'Instance.</AlertDescription>
      </Alert>
    );
  }
  return <PageAdministration />;
}

function PageAdministration() {
  const { onglet = ONGLET_PAR_DEFAUT } = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });
  return (
    <>
      <PageHeader title="Administration" description="Réservée aux administrateurs." />
      <ATraiter />
      <Tabs
        value={onglet}
        onValueChange={(value) => navigate({ search: value === ONGLET_PAR_DEFAUT ? {} : { onglet: value } })}
        className="gap-4"
      >
        <TabsList>
          {ONGLETS.map((o) => (
            <TabsTrigger key={o.id} value={o.id}>
              {o.label}
            </TabsTrigger>
          ))}
        </TabsList>
        {ONGLETS.map((o) => (
          <TabsContent key={o.id} value={o.id}>
            {o.contenu}
          </TabsContent>
        ))}
      </Tabs>
    </>
  );
}
