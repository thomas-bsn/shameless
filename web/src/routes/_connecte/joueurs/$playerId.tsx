import { createFileRoute, useRouter } from "@tanstack/react-router";
import { $api } from "@/api/client";
import { HeroDuJoueur, SectionsDeLaFiche } from "@/components/fiche-joueur";
import { ErrorAlert, LoadingRows } from "@/components/query-state";

export const Route = createFileRoute("/_connecte/joueurs/$playerId")({
  component: FicheJoueur,
});

function FicheJoueur() {
  const { playerId } = Route.useParams();
  const router = useRouter();
  const retour = () => (router.history.canGoBack() ? router.history.back() : router.navigate({ to: "/joueurs" }));
  const player = $api.useQuery("get", "/players/{playerId}", { params: { path: { playerId: Number(playerId) } } });

  return (
    <div className="flex flex-col gap-6">
      <button type="button" onClick={retour} className="self-start text-sm text-muted-foreground hover:text-foreground">
        ← Retour
      </button>
      {player.isPending ? (
        <LoadingRows />
      ) : player.isError ? (
        <ErrorAlert error={player.error} />
      ) : (
        <>
          <HeroDuJoueur player={player.data} />
          <SectionsDeLaFiche playerId={player.data.id} />
        </>
      )}
    </div>
  );
}
