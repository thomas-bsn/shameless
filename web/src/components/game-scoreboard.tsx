import { $api, type Schemas } from "@/api/client";
import { type Participant, type Side, sideName, TableauEquipe } from "@/components/game-equipe-table";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { IconeDeJeu } from "@/components/icone-de-jeu";
import { useCatalogue } from "@/lib/catalogue";
import { csTotal, kdaLabel, millierLabel, noteTone, parMinuteLabel, posteLabel, statLabel, trierParRole } from "@/lib/game";
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
    { team: blue[0], side: "blue" as const, players: trierParRole(blue[1]) },
    { team: red[0], side: "red" as const, players: trierParRole(red[1]) },
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
  const catalogue = useCatalogue();
  const maxDegats = Math.max(1, ...sidesOf(game).flatMap((s) => s.players.map((p) => p.stats?.damageToChampions ?? 0)));

  return (
    <TableauEquipe
      team={team}
      side={side}
      resume={game.winner === team ? "Victoire" : "Défaite"}
      colonnes={[
        { titre: "Sorts" },
        { titre: "Objets" },
        { titre: "K/D/A" },
        { titre: "PK", droite: true },
        { titre: "CS", droite: true },
        { titre: "Or", droite: true },
        { titre: "Dégâts" },
        { titre: "Note", droite: true },
      ]}
      players={players}
      colonneAvant="Rôle"
      ligne={(p) => {
        const estMvp = titres.mvp.includes(p.playerId);
        const estDofd = titres.dofd.includes(p.playerId);
        const mono = "font-mono tabular-nums";
        const d = p.details;
        const sorts = [d?.spell1Id ?? p.stats?.spell1Id, d?.spell2Id ?? p.stats?.spell2Id];
        const objets = d?.items ?? p.stats?.items ?? null;
        return {
          avant: posteLabel(d?.role ?? p.stats?.position) ?? "–",
          className: cn(estMvp && ENTOURE_MVP, estDofd && !estMvp && ENTOURE_DOFD),
          badges: (
            <>
              {estMvp && <Badge className="bg-yellow-400 text-black">MVP</Badge>}
              {estDofd && <Badge variant="destructive">DOFD</Badge>}
              {p.badge === "MeilleurePerf" && <Badge className="bg-emerald-500 text-black">Meilleure perf</Badge>}
              {p.badge === "Ace" && <Badge className="bg-fuchsia-500 text-black">ACE</Badge>}
            </>
          ),
          cellules: [
            <div className="flex gap-1">
              {sorts.map((id, i) => (
                <IconeDeJeu key={i} id={id} element={id == null ? undefined : catalogue.sort(id)} />
              ))}
            </div>,
            <Objets items={objets} roleItem={d?.objetDeRole ?? null} />,
            <span className={mono}>{p.stats ? kdaLabel(p.stats) : "–"}</span>,
            <span className={mono}>{d?.partDesKills == null ? "–" : `${Math.round(d.partDesKills * 100)} %`}</span>,
            <div className={cn("flex flex-col", mono)}>
              <span>{statLabel(p.stats ? csTotal(p.stats) : null)}</span>
              <span className="text-xs text-muted-foreground">{parMinuteLabel(d?.csParMinute ?? null, 1)}</span>
            </div>,
            <div className={cn("flex flex-col", mono)}>
              <span>{millierLabel(p.stats?.goldEarned ?? null)}</span>
              <span className="text-xs text-muted-foreground">{parMinuteLabel(d?.orParMinute ?? null)}</span>
            </div>,
            <Degats value={p.stats?.damageToChampions ?? null} max={maxDegats} side={side} />,
            <div className={cn("flex flex-col items-end", mono)}>
              <span className={cn(p.performance && toneClass[noteTone(p.performance.note)])}>
                {p.performance ? `${p.performance.note}/100` : "–"}
              </span>
              {p.place != null && <span className="text-xs text-muted-foreground">#{p.place}</span>}
            </div>,
          ],
        };
      }}
    />
  );
}

/** Les sept emplacements d'objets (six objets et la babiole), puis l'objet de rôle à part, liseré doré. */
function Objets({
  items,
  roleItem,
}: {
  items: Array<number | null> | null;
  roleItem: Schemas["ObjetDeRole"] | null;
}) {
  const catalogue = useCatalogue();
  if (!items && !roleItem) return <span>–</span>;
  const emplacements = Array.from({ length: 7 }, (_, i) => items?.[i] ?? null);
  return (
    <div className="flex items-center gap-2">
      <ul className="flex gap-0.5">
        {emplacements.map((id, i) => (
          <li key={i}>
            <IconeDeJeu id={id} element={id == null ? undefined : catalogue.objet(id)} />
          </li>
        ))}
      </ul>
      {roleItem && (
        <div role="group" aria-label="Objet de rôle" className="flex flex-col items-center gap-0.5">
          <IconeDeJeu
            id={roleItem.id}
            element={catalogue.objet(roleItem.id)}
            className="border-2 border-yellow-400"
          />
          {roleItem.etat && (
            <span className="text-[10px] leading-3 text-yellow-400">
              {roleItem.etat === "Terminee" ? "Quête terminée" : "Quête en cours"}
            </span>
          )}
        </div>
      )}
    </div>
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
