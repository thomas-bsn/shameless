import { useState } from "react";
import { createFileRoute, Link } from "@tanstack/react-router";
import { $api, type Schemas } from "@/api/client";
import { GameClips } from "@/components/game-clips";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PronosticsDeLaGame } from "@/components/pronostics-de-la-game";
import { VotesDeLaGame } from "@/components/votes-de-la-game";
import { dureeLabel, kdaLabel } from "@/lib/game";
import { gameWinner } from "@/lib/history";
import { sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";
import { dateHeureLabel } from "@/lib/dates";

/** La page d'une Game : c'est le lien de récap posté par le bot Discord. */
export const Route = createFileRoute("/_connecte/games/$gameId")({
  component: GamePage,
});

type Recap = Schemas["GameRecapResponse"];

function GamePage() {
  const { gameId } = Route.useParams();
  const game = $api.useQuery("get", "/games/{id}", { params: { path: { id: Number(gameId) } } });

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <Link to="/historique" className="text-sm text-muted-foreground hover:text-foreground">
        ← Historique
      </Link>
      {game.isPending ? <LoadingRows /> : game.isError ? <ErrorAlert error={game.error} /> : <Recit game={game.data} />}
    </div>
  );
}

function Recit({ game }: { game: Recap }) {
  const [tab, setTab] = useState("resume");
  const votes = $api.useQuery("get", "/games/{id}/votes", { params: { path: { id: game.id } } });
  const winner = gameWinner(game);
  const mvp = new Set(votes.data?.statut === "clos" ? votes.data.mvp.gagnants : []);
  const dofd = new Set(votes.data?.statut === "clos" ? votes.data.dofd.gagnants : []);
  const aOnBlue = game.teamASide !== "red";
  const title = game.seriesId == null ? `Game #${game.id}` : `Game ${game.gameNumber} · Série BO${game.bestOf}`;

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <p className="text-sm text-muted-foreground">{dateHeureLabel(game.playedAt)}</p>
          <h1 className="text-2xl font-bold">{title}</h1>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          {game.seriesId != null && (
            <span className="font-semibold">
              Équipe A {game.teamAWins} – {game.teamBWins} Équipe B
            </span>
          )}
          {winner ? (
            <Badge variant="secondary">Équipe {winner} gagne</Badge>
          ) : (
            <Badge variant="outline">En cours</Badge>
          )}
        </div>
      </div>
      <div className="flex flex-wrap gap-4 text-sm text-muted-foreground">
        {game.match?.durationSeconds != null && <span>{dureeLabel(game.match.durationSeconds)}</span>}
        {game.seriesId != null && (
          <Link to="/series/$seriesId" params={{ seriesId: String(game.seriesId) }} className="hover:text-foreground">
            Voir la Série
          </Link>
        )}
      </div>
      <nav aria-label="Statistiques de la game" className="flex flex-wrap gap-2 border-b border-border/60 pb-3">{[["resume", "Résumé"], ["degats", "Dégâts"], ["objectifs", "Objectifs"], ["vision", "Vision"], ["replay", "Replay"]].map(([value, label]) => <button key={value} type="button" disabled={value === "replay"} aria-current={tab === value ? "page" : undefined} title={value === "replay" ? "Le replay 3D est indisponible car les positions des joueurs ne sont pas disponibles." : undefined} onClick={() => setTab(value)} className={cn("rounded-lg px-3 py-2 text-sm", value === tab ? "bg-primary/10 font-semibold text-primary" : "text-muted-foreground hover:bg-muted", value === "replay" && "cursor-not-allowed opacity-50")}>{label}{value === "replay" && <span className="ml-1 text-[10px]">Prévu · positions absentes</span>}</button>)}</nav>
      {tab === "resume" ? <div className="grid gap-4 md:grid-cols-2">
        <Equipe
          team={aOnBlue ? "A" : "B"}
          side="blue"
          players={aOnBlue ? game.teamA : game.teamB}
          won={winner === (aOnBlue ? "A" : "B")}
          mvp={mvp} dofd={dofd}
        />
        <Equipe
          team={aOnBlue ? "B" : "A"}
          side="red"
          players={aOnBlue ? game.teamB : game.teamA}
          won={winner === (aOnBlue ? "B" : "A")}
          mvp={mvp} dofd={dofd}
        />
      </div> : <Statistiques game={game} type={tab} />}
      <VotesDeLaGame gameId={game.id} />
      <PronosticsDeLaGame gameId={game.id} />
      <GameClips gameId={game.id} />
    </>
  );
}

function Statistiques({ game, type }: { game: Recap; type: string }) {
  const blueIsA = game.teamASide !== "red";
  const teams = [{ name: `Équipe ${blueIsA ? "A" : "B"} · Blue`, side: "blue", players: blueIsA ? game.teamA : game.teamB }, { name: `Équipe ${blueIsA ? "B" : "A"} · Red`, side: "red", players: blueIsA ? game.teamB : game.teamA }];
  const rows = type === "degats" ? [["damageToChampions", "Dégâts infligés"], ["damageTaken", "Dégâts subis"]] as const : type === "objectifs" ? [["turretKills", "Tours"], ["dragonKills", "Dragons"], ["baronKills", "Barons"], ["damageToObjectives", "Dégâts aux objectifs"]] as const : [["visionScore", "Score de vision"], ["wardsPlaced", "Balises posées"], ["wardsKilled", "Balises détruites"], ["controlWardsBought", "Balises de contrôle"]] as const;
  const durationMinutes = game.match?.durationSeconds ? game.match.durationSeconds / 60 : null;
  if (teams.every((team) => team.players.every((player) => !player.stats))) return <p className="rounded-xl border border-border/60 bg-card/50 p-6 text-sm text-muted-foreground">Les statistiques détaillées sont absentes pour cette game.</p>;
  return <div className="grid gap-4 md:grid-cols-2">{teams.map((team) => <Card key={team.side} role="group" aria-label={team.name}><CardHeader><CardTitle className={cn("text-sm", sideTextClass(team.side as "blue" | "red"))}>{team.name}</CardTitle><div className="grid grid-cols-2 gap-2">{rows.map(([key, label]) => { const values = team.players.map((player) => player.stats?.[key]); const sum = values.reduce<number>((total, value) => total + (value ?? 0), 0); const available = values.length > 0 && values.every((value) => value != null); return <div key={key} className="rounded-lg bg-muted/40 p-2"><p className="text-[10px] text-muted-foreground">{label}</p><p className="font-mono text-sm">{available ? sum.toLocaleString("fr-FR") : "—"}</p></div>; })}</div></CardHeader><CardContent className="flex flex-col gap-2">{team.players.map((player) => <div key={player.playerId} className="grid grid-cols-[1fr_repeat(4,minmax(48px,auto))] items-center gap-2 border-t border-border/50 py-2 text-xs"><span className="truncate font-medium">{player.prenom}</span>{rows.map(([key, label]) => { const value = player.stats?.[key]; const perMinute = key === "damageToChampions" && value != null && durationMinutes ? Math.round(value / durationMinutes) : null; const share = key === "damageToChampions" && value != null ? `${Math.round(value / Math.max(1, team.players.reduce((sum, p) => sum + (p.stats?.damageToChampions ?? 0), 0)) * 100)}%` : null; return <span key={key} aria-label={`${label} ${player.prenom}`} className="text-right font-mono">{value == null ? "—" : value.toLocaleString("fr-FR")}{type === "degats" && key === "damageToChampions" && <> · <span aria-label={`Dégâts par minute ${player.prenom}`}>{perMinute == null ? "—" : `${perMinute}/min`}</span> · <span aria-label={`Part des dégâts de l'équipe ${player.prenom}`}>{share}</span></>}</span>; })}</div>)}</CardContent></Card>)}</div>;
}

function Equipe({
  team,
  side,
  players,
  won,
  mvp,
  dofd,
}: {
  team: string;
  side: "blue" | "red";
  players: Schemas["GameHistoryParticipantDto"][];
  won: boolean;
  mvp: Set<number>;
  dofd: Set<number>;
}) {
  const label = `Équipe ${team} · ${side === "blue" ? "Blue" : "Red"} side`;
  const maxDamage = Math.max(1, ...players.map((p) => p.stats?.damageToChampions ?? 0));
  const valeur = (n: number | null | undefined) => n == null ? "—" : n.toLocaleString("fr-FR");

  return (
    <Card role="group" aria-label={label} className={cn("overflow-hidden", won && "border-emerald-600/50 dark:border-emerald-400/50")}>
      <CardHeader className="pb-3">
        <CardTitle className={cn("text-sm", sideTextClass(side))}>{label}</CardTitle>
      </CardHeader>
      <CardContent className="overflow-x-auto px-3">
        {gameSansStats(players) && <p className="mb-3 text-sm text-muted-foreground">Game saisie à la main : les stats détaillées et les notes de performance ne sont pas disponibles.</p>}
        <table className="w-full min-w-[620px] text-left text-xs">
          <thead className="text-muted-foreground"><tr><th className="pb-2">Joueur</th><th>Champion · niv.</th><th>K / D / A</th><th>CS</th><th>Or</th><th>Dégâts</th><th>Note</th></tr></thead>
          <tbody className="divide-y divide-border/60">{players.map((p) => { const stats = p.stats; const damage = stats?.damageToChampions ?? null; const titles = [mvp.has(p.playerId) ? "MVP" : null, dofd.has(p.playerId) ? "DOFD" : null].filter(Boolean); return <tr key={p.playerId} className={cn("h-11", titles.length > 0 && "outline outline-1 outline-primary/70 outline-offset-[-2px]")}>
            <th scope="row" className="pr-2 font-medium"><span>{p.prenom}</span>{titles.map((title) => <Badge key={title} aria-label={`${title} ${p.prenom}`} className="ml-1" variant="outline">{title}</Badge>)}</th><td>{p.championId ?? "—"}{stats?.champLevel != null && <span className="ml-1 text-muted-foreground">· {stats.champLevel}</span>}</td>
            <td className="font-mono">{stats ? kdaLabel(stats) : "—"}</td><td className="font-mono">{stats ? valeur((stats.minionsKilled ?? 0) + (stats.neutralMinionsKilled ?? 0)) : "—"}</td>
            <td className="font-mono">{valeur(stats?.goldEarned)}</td><td><span className="font-mono">{valeur(damage)}</span><span className="ml-2 inline-block h-1.5 rounded bg-primary/70 align-middle" style={{ width: `${damage == null ? 0 : Math.max(4, damage / maxDamage * 48)}px` }} /></td>
            <td>{p.performance ? <Badge variant="outline">{p.performance.note}/100</Badge> : "—"}</td>
          </tr>; })}</tbody>
        </table>
      </CardContent>
    </Card>
  );
}

function gameSansStats(players: Schemas["GameHistoryParticipantDto"][]) {
  return players.length > 0 && players.every((p) => p.stats == null);
}
