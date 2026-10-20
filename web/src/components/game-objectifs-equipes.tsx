import type { Schemas } from "@/api/client";
import { sidesOf } from "@/components/game-scoreboard";
import { sideName } from "@/components/game-equipe-table";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { statLabel } from "@/lib/game";
import { sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";

type Objectifs = Schemas["ObjectifsDEquipe"];

const COMPTEURS: Array<[string, (o: Objectifs) => number | null]> = [
  ["Dragons", (o) => o.dragonKills],
  ["Barons", (o) => o.baronKills],
  ["Hérauts", (o) => o.riftHeraldKills],
  ["Larves du Néant", (o) => o.hordeKills],
  ["Tourelles", (o) => o.towerKills],
  ["Inhibiteurs", (o) => o.inhibitorKills],
];

const PREMIERS: Array<[string, (o: Objectifs) => boolean | null]> = [
  ["Premier sang", (o) => o.firstBlood],
  ["Première tourelle", (o) => o.firstTower],
  ["Premier inhibiteur", (o) => o.firstInhibitor],
];

/** Les objectifs de chaque équipe ; rien pour une game saisie à la main (le Rapport ne les donne pas). */
export function ObjectifsDesEquipes({ game }: { game: Schemas["GameRecapResponse"] }) {
  const cotes = sidesOf(game).map((c) => ({
    ...c,
    objectifs: c.team === "A" ? game.teamAObjectifs : game.teamBObjectifs,
  }));
  if (cotes.every((c) => c.objectifs == null)) return null;

  return (
    <div className="grid gap-4 md:grid-cols-2">
      {cotes.map(({ team, side, objectifs }) =>
        objectifs ? (
          <Card key={team} role="group" aria-label={`Objectifs de l'Équipe ${team}`} className="gap-2 py-4">
            <CardHeader className="px-4">
              <CardTitle className={cn("text-sm", sideTextClass(side))}>
                Objectifs · Équipe {team} · {sideName(side)} side
              </CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-3 px-4">
              <dl className="flex flex-wrap gap-x-5 gap-y-1">
                {COMPTEURS.map(([nom, lire]) => (
                  <div key={nom} className="flex items-baseline gap-1.5">
                    <dt className="text-xs text-muted-foreground">{nom}</dt>
                    <dd className="font-mono font-semibold tabular-nums">{statLabel(lire(objectifs))}</dd>
                  </div>
                ))}
              </dl>
              <div className="flex flex-wrap gap-1.5">
                {PREMIERS.filter(([, lire]) => lire(objectifs) === true).map(([nom]) => (
                  <Badge key={nom} variant="outline">
                    {nom}
                  </Badge>
                ))}
              </div>
            </CardContent>
          </Card>
        ) : null,
      )}
    </div>
  );
}
