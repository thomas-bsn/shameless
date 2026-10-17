import { createFileRoute, Link } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { $api } from "@/api/client";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { CostTierBadge, RankBadge, RoleIcon } from "@/components/rank";
import { TitresDuJoueur } from "@/components/titres-du-joueur";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { roleLabel } from "@/lib/ranks";

export const Route = createFileRoute("/_connecte/joueurs/$playerId")({
  component: FicheJoueur,
});

function FicheJoueur() {
  const { playerId } = Route.useParams();
  const player = $api.useQuery("get", "/players/{playerId}", { params: { path: { playerId: Number(playerId) } } });

  return (
    <div className="flex flex-col gap-6">
      <Link to="/joueurs" className="text-sm text-muted-foreground hover:text-foreground">
        ← Joueurs
      </Link>
      {player.isPending ? (
        <LoadingRows />
      ) : player.isError ? (
        <ErrorAlert error={player.error} />
      ) : (
        <>
          <div>
            <h1 className="text-2xl font-bold">{player.data.prenom}</h1>
            <p className="text-muted-foreground">{player.data.riotId}</p>
          </div>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Stat title="Rang SoloQ">
              <RankBadge tier={player.data.rankTier} division={player.data.rankDivision} lp={player.data.lp} />
            </Stat>
            <Stat title="Peak">
              {player.data.peakTier ? (
                <div className="flex flex-col items-start gap-1">
                  <RankBadge tier={player.data.peakTier} division={player.data.peakDivision} lp={player.data.peakLp} />
                  {player.data.peakSeason && (
                    <span className="text-xs text-muted-foreground">{player.data.peakSeason}</span>
                  )}
                </div>
              ) : (
                <span className="text-muted-foreground">Non renseigné</span>
              )}
            </Stat>
            <Stat title="Cost">
              <div className="flex items-center gap-2">
                <CostTierBadge cost={player.data.currentCost} className="text-lg" />
                <span className="text-sm text-muted-foreground">{player.data.currentCost} pts</span>
              </div>
            </Stat>
            <Stat title="Rôle principal">
              {roleLabel(player.data.mainRole) ? (
                <span className="flex items-center gap-2">
                  <RoleIcon role={player.data.mainRole} />
                  {roleLabel(player.data.mainRole)}
                </span>
              ) : (
                <span className="text-muted-foreground">Inconnu</span>
              )}
            </Stat>
          </div>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <TitresDuJoueur playerId={player.data.id} />
          </div>
        </>
      )}
    </div>
  );
}

function Stat({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm text-muted-foreground">{title}</CardTitle>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}
