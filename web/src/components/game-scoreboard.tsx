import { $api, type Schemas } from "@/api/client";
import { type Participant, type Side, sideName, TableauEquipe } from "@/components/game-equipe-table";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { csTotal, kdaLabel, millierLabel, noteTone, statLabel } from "@/lib/game";
import { sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";
import { AvatarJoueur } from "@/components/avatar-joueur";


/** Le MVP (or) et le DOFD (rouge) sont entourés : une bordure sur les cellules de leur ligne. */
const ENTOURE_MVP =
  "[&>td]:border-y-2 [&>td]:border-yellow-400 [&>td:first-child]:border-l-2 [&>td:last-child]:border-r-2";
const ENTOURE_DOFD =
  "[&>td]:border-y-2 [&>td]:border-red-500 [&>td:first-child]:border-l-2 [&>td:last-child]:border-r-2";

const toneClass = {
  good: "text-emerald-400",
  mid: "text-foreground",
  bad: "text-red-400",
} as const;

/** Les deux équipes d'une Game, côté bleu puis côté rouge, avec leur issue. */
export function sidesOf(game: Schemas["GameRecapResponse"]) {
  const aOnBlue = game.teamASide !== "red";
  const [blue, red] = aOnBlue
    ? ([["A", game.teamA], ["B", game.teamB]] as const)
    : ([["B", game.teamB], ["A", game.teamA]] as const);
  return [
    { team: blue[0], side: "blue" as const, players: blue[1] },
    { team: red[0], side: "red" as const, players: red[1] },
  ];
}

/** Le MVP et le DOFD de la Game, d'après les votes clos (aucun tant qu'ils ne le sont pas). */
function useTitres(gameId: number) {
  const votes = $api.useQuery("get", "/games/{id}/votes", { params: { path: { id: gameId } } });
  return {
    mvp: votes.data?.mvp.gagnants ?? [],
    dofd: votes.data?.dofd.gagnants ?? [],
  };
}

/** Le tableau d'une équipe façon client de LoL : champion, K/D/A, CS, or, dégâts et note sur 100. */
export function ScoreboardEquipe({
  game,
  team,
  side,
  players,
}: {
  game: Schemas["GameRecapResponse"];
  team: string;
  side: Side;
  players: Participant[];
}) {
  const titres = useTitres(game.id);
  const maxDegats = Math.max(1, ...sidesOf(game).flatMap((s) => s.players.map((p) => p.stats?.damageToChampions ?? 0)));

  return (
    <TableauEquipe
      team={team}
      side={side}
      resume={game.winner === team ? "Victoire" : "Défaite"}
      colonnes={[
        { titre: "K/D/A" },
        { titre: "CS", droite: true },
        { titre: "Or", droite: true },
        { titre: "Dégâts" },
        { titre: "Note", droite: true },
      ]}
      players={players}
      ligne={(p) => {
        const estMvp = titres.mvp.includes(p.playerId);
        const estDofd = titres.dofd.includes(p.playerId);
        const mono = "font-mono tabular-nums";
        return {
          className: cn(estMvp && ENTOURE_MVP, estDofd && !estMvp && ENTOURE_DOFD),
          badges: (
            <>
              {estMvp && <Badge className="bg-yellow-400 text-black">MVP</Badge>}
              {estDofd && <Badge variant="destructive">DOFD</Badge>}
            </>
          ),
          cellules: [
            <span className={mono}>{p.stats ? kdaLabel(p.stats) : "–"}</span>,
            <span className={mono}>{statLabel(p.stats ? csTotal(p.stats) : null)}</span>,
            <span className={mono}>{millierLabel(p.stats?.goldEarned ?? null)}</span>,
            <Degats value={p.stats?.damageToChampions ?? null} max={maxDegats} side={side} />,
            <span className={cn(mono, p.performance && toneClass[noteTone(p.performance.note)])}>
              {p.performance ? `${p.performance.note}/100` : "–"}
            </span>,
          ],
        };
      }}
    />
  );
}

/** Les dégâts aux champions et une barre proportionnelle au plus gros total de la Game. */
function Degats({ value, max, side }: { value: number | null; max: number; side: Side }) {
  return (
    <div className="flex min-w-24 flex-col gap-1">
      <span className="font-mono tabular-nums">{millierLabel(value)}</span>
      {value != null && (
        <progress
          aria-hidden
          value={value}
          max={max}
          className={cn(
            "h-1 w-full appearance-none overflow-hidden rounded-full bg-muted [&::-moz-progress-bar]:bg-current [&::-webkit-progress-bar]:bg-muted [&::-webkit-progress-value]:bg-current",
            sideTextClass(side),
          )}
        />
      )}
    </div>
  );
}

/** Les équipes d'une Game sans stats : juste les noms des joueurs. */
export function EquipeSansStats({ team, side, players, won }: { team: string; side: Side; players: Participant[]; won: boolean }) {
  const label = `Équipe ${team} · ${sideName(side)} side`;
  return (
    <Card role="group" aria-label={label}>
      <CardHeader className="flex-row items-center justify-between gap-2">
        <CardTitle className={cn("text-sm", sideTextClass(side))}>{label}</CardTitle>
        <span className="text-sm font-medium text-muted-foreground">{won ? "Victoire" : "Défaite"}</span>
      </CardHeader>
      <CardContent>
        <ul className="flex flex-wrap gap-2">
          {players.map((p) => (
            <li key={p.playerId}>
              <Badge variant="outline" className="gap-1.5 pl-1">
                <AvatarJoueur prenom={p.prenom} avatarUrl={p.avatarUrl} className="size-5 text-[9px]" />
                {p.prenom}
              </Badge>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
