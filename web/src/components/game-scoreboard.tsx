import { $api, type Schemas } from "@/api/client";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useChampions } from "@/lib/draft-champions-queries";
import { csTotal, kdaLabel, millierLabel, noteTone, posteLabel, statLabel } from "@/lib/game";
import { sideBorderClass, sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";

type Participant = Schemas["GameHistoryParticipantDto"];
type Side = "blue" | "red";

const sideName = (side: Side) => (side === "blue" ? "Blue" : "Red");

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
  const champions = useChampions();
  const label = `Équipe ${team} · ${sideName(side)} side`;
  const issue = game.winner === team ? "Victoire" : "Défaite";
  const maxDegats = Math.max(1, ...sidesOf(game).flatMap((s) => s.players.map((p) => p.stats?.damageToChampions ?? 0)));
  const catalogue = champions.data ?? [];

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between gap-2">
        <CardTitle className={cn("text-sm", sideTextClass(side))}>{label}</CardTitle>
        <span className="text-sm font-medium text-muted-foreground">{issue}</span>
      </CardHeader>
      <CardContent className="overflow-x-auto">
        <table aria-label={label} className="w-full min-w-[640px] border-separate border-spacing-y-1 text-sm">
          <thead className="text-xs text-muted-foreground">
            <tr>
              <th scope="col" className="px-2 text-left font-medium">
                Joueur
              </th>
              <th scope="col" className="px-2 text-left font-medium">
                K/D/A
              </th>
              <th scope="col" className="px-2 text-right font-medium">
                CS
              </th>
              <th scope="col" className="px-2 text-right font-medium">
                Or
              </th>
              <th scope="col" className="px-2 text-left font-medium">
                Dégâts
              </th>
              <th scope="col" className="px-2 text-right font-medium">
                Note
              </th>
            </tr>
          </thead>
          <tbody>
            {players.map((p) => {
              const champion = catalogue.find((c) => c.id === p.championId);
              const estMvp = titres.mvp.includes(p.playerId);
              const estDofd = titres.dofd.includes(p.playerId);
              return (
                <tr
                  key={p.playerId}
                  aria-label={p.prenom}
                  className={cn(
                    "[&>td]:bg-muted/30 [&>td:first-child]:rounded-l-lg [&>td:last-child]:rounded-r-lg",
                    estMvp && ENTOURE_MVP,
                    estDofd && !estMvp && ENTOURE_DOFD,
                  )}
                >
                  <td className="px-2 py-2">
                    <div className="flex items-center gap-3">
                      <ChampionCarre championId={p.championId} nom={champion?.name} image={champion?.imageUrl} niveau={p.stats?.champLevel} side={side} />
                      <div className="flex min-w-0 flex-col">
                        <span className="flex flex-wrap items-center gap-1 font-medium">
                          <span className="truncate">{p.prenom}</span>
                          {estMvp && <Badge className="bg-yellow-400 text-black">MVP</Badge>}
                          {estDofd && <Badge variant="destructive">DOFD</Badge>}
                        </span>
                        <span className="text-xs text-muted-foreground">
                          {[champion?.name ?? p.championId, posteLabel(p.stats?.position)]
                            .filter(Boolean)
                            .join(" · ") || "Champion inconnu"}
                        </span>
                      </div>
                    </div>
                  </td>
                  <td className="px-2 font-mono tabular-nums">{p.stats ? kdaLabel(p.stats) : "–"}</td>
                  <td className="px-2 text-right font-mono tabular-nums">{statLabel(p.stats ? csTotal(p.stats) : null)}</td>
                  <td className="px-2 text-right font-mono tabular-nums">{millierLabel(p.stats?.goldEarned ?? null)}</td>
                  <td className="px-2">
                    <Degats value={p.stats?.damageToChampions ?? null} max={maxDegats} side={side} />
                  </td>
                  <td className={cn("px-2 text-right font-mono tabular-nums", p.performance && toneClass[noteTone(p.performance.note)])}>
                    {p.performance ? `${p.performance.note}/100` : "–"}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </CardContent>
    </Card>
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

/** L'illustration du champion (image du jeu), sinon un carré avec son initiale ; le niveau en coin. */
function ChampionCarre({
  championId,
  nom,
  image,
  niveau,
  side,
}: {
  championId: string | null;
  nom?: string;
  image?: string;
  niveau?: number | null;
  side: Side;
}) {
  return (
    <span className={cn("relative flex size-10 shrink-0 items-center justify-center rounded-md border-2 bg-muted text-sm font-semibold", sideBorderClass(side))}>
      {image ? <img src={image} alt="" className="size-full rounded-sm object-cover" /> : <span aria-hidden>{(nom ?? championId ?? "?").charAt(0)}</span>}
      {niveau != null && (
        <span
          data-slot="niveau"
          className="absolute -right-1 -bottom-1 rounded bg-background px-1 font-mono text-[10px] leading-4 tabular-nums"
        >
          {niveau}
        </span>
      )}
    </span>
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
              <Badge variant="outline">{p.prenom}</Badge>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
