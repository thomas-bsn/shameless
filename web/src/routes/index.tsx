import { useQuery } from "@tanstack/react-query";
import { createFileRoute, Link } from "@tanstack/react-router";
import { $api } from "@/api/client";
import { ApiStatus } from "@/components/api-status";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { sessionQuery } from "@/lib/session";

export const Route = createFileRoute("/")({
  component: Home,
});

function Home() {
  const session = useQuery(sessionQuery);

  return (
    <section className="flex flex-col items-center gap-4 py-12 text-center">
      <img src="/logo.png" alt="" className="size-40 rounded-full object-cover" />
      <h1 className="text-4xl font-bold">Shameless</h1>
      <p className="text-muted-foreground">Les customs 5v5 entre potes : joueurs, séries, classements et historique.</p>
      {session.data && <SoireeOuverte />}
      <ApiStatus />
    </section>
  );
}

/** La Soirée ouverte, ou l'invitation à en ouvrir une. */
function SoireeOuverte() {
  const open = $api.useQuery("get", "/soirees/open");
  if (!open.isSuccess) return null;

  const enCours = open.data.soiree !== null;
  return (
    <Card className="w-full max-w-md">
      <CardContent className="flex flex-col items-center gap-3">
        <p>{enCours ? "Une Soirée est en cours." : "Aucune Soirée n'est ouverte."}</p>
        <Button asChild>
          <Link to="/soiree">{enCours ? "Aller à la Soirée" : "Ouvrir une Soirée"}</Link>
        </Button>
      </CardContent>
    </Card>
  );
}
