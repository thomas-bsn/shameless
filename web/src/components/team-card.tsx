import { Crown } from "lucide-react";
import type { Schemas } from "@/api/client";
import { RankBadge } from "@/components/rank";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { points } from "@/lib/cost";
import { parRole } from "@/lib/picker";
import { cn } from "@/lib/utils";

export const roleLabels: Record<NonNullable<Schemas["Role"]>, string> = {
  Top: "Top",
  Jungle: "Jungle",
  Mid: "Mid",
  Adc: "ADC",
  Support: "Support",
};

/**
 * Une Équipe et ses joueurs (rôle de l'Autorole, rang, cost), du top au support quand les rôles
 * sont attribués. Sert au Picker et à l'écran Série.
 */
export function TeamCard({
  equipe,
  players,
  cost,
  capitaine,
  highlighted = false,
}: {
  equipe: "A" | "B";
  players: Schemas["GameParticipantDto"][];
  /** Le cost total de l'Équipe, affiché quand il est fourni. */
  cost?: number;
  /** Le capitaine de la Draft, marqué d'une couronne. */
  capitaine?: number | null;
  highlighted?: boolean;
}) {
  return (
    <Card role="group" aria-label={`Équipe ${equipe}`} className={cn(highlighted && "border-primary")}>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Équipe {equipe}</CardTitle>
        {cost !== undefined && <span className="text-sm text-muted-foreground">{points(cost)}</span>}
      </CardHeader>
      <CardContent>
        {players.length === 0 ? (
          <p className="text-sm text-muted-foreground">Personne pour l'instant.</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {parRole(players).map((p) => (
              <li key={p.playerId} className="flex items-center justify-between gap-2 text-sm">
                <span className="flex items-center gap-2 truncate">
                  {p.role && (
                    <Badge variant="outline" className="w-16 justify-center">
                      {roleLabels[p.role]}
                    </Badge>
                  )}
                  {p.prenom}
                  {p.playerId === capitaine && <Crown aria-label="Capitaine" className="size-4 text-amber-600 dark:text-amber-400" />}
                </span>
                <span className="flex items-center gap-2">
                  <RankBadge tier={p.rankTier} division={p.rankDivision} lp={null} className="text-[10px]" />
                  <span className="text-xs text-muted-foreground">{points(p.cost ?? 0)}</span>
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
