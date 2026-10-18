import { Link } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert } from "@/components/query-state";
import { CostTierBadge, RankBadge, RoleIcon } from "@/components/rank";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { points } from "@/lib/cost";
import { dateHeureLabel } from "@/lib/dates";
import { useChampions } from "@/lib/draft-champions-queries";
import {
  COST_MAX,
  COST_MIN,
  COURBE,
  decimalLabel,
  entierLabel,
  kdaDetailLabel,
  pointsDeLaCourbe,
  pourcentLabel,
  yDuCost,
} from "@/lib/fiche";
import { noteTone, posteLabel } from "@/lib/game";
import { roleLabel } from "@/lib/ranks";
import { cn } from "@/lib/utils";

type Fiche = Schemas["FicheJoueur"];
type Stats = Schemas["StatsDeCustoms"];

const GRADUATIONS = Array.from({ length: COST_MAX - COST_MIN + 1 }, (_, i) => COST_MIN + i);

const noteClass = { good: "text-emerald-400", mid: "text-foreground", bad: "text-red-400" } as const;

/** Les sections de la fiche tirées de `GET /players/{id}/fiche` et de l'historique mensuel du cost. */
export function SectionsDeLaFiche({ playerId }: { playerId: number }) {
  const fiche = $api.useQuery("get", "/players/{playerId}/fiche", { params: { path: { playerId } } });

  if (fiche.isPending) return <Skeleton className="h-64 w-full" />;
  if (fiche.isError) return <ErrorAlert error={fiche.error} />;

  return (
    <div className="grid gap-4 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
      <div className="flex min-w-0 flex-col gap-4">
        <StatsEnCustoms stats={fiche.data.stats} />
        <CoutMoisParMois playerId={playerId} />
        <DernieresGames games={fiche.data.games} />
      </div>
      <div className="flex min-w-0 flex-col gap-4">
        <Champions champions={fiche.data.champions} />
        <Roles roles={fiche.data.roles} />
      </div>
    </div>
  );
}

/** Identité, rangs SoloQ, Flex et peak, titres et tier de cost en grand. */
export function HeroDuJoueur({ player }: { player: Schemas["PlayerDto"] }) {
  const fiche = $api.useQuery("get", "/players/{playerId}/fiche", { params: { path: { playerId: player.id } } }).data;
  const role = roleLabel(player.mainRole);

  return (
    <Card>
      <CardContent className="flex flex-wrap items-center justify-between gap-6">
        <div className="flex min-w-0 items-center gap-4">
          <span aria-hidden className="flex size-14 shrink-0 items-center justify-center rounded-full border bg-muted text-xl font-semibold">
            {player.prenom.charAt(0)}
          </span>
          <div className="flex min-w-0 flex-col gap-2">
            <div>
              <h1 className="text-2xl font-bold">{player.prenom}</h1>
              <p className="text-sm text-muted-foreground">
                <span>{player.riotId}</span>
                {fiche?.discord && <span> · Discord lié</span>}
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
              <Rang
                label="SoloQ"
                rang={player.rankTier ? { tier: player.rankTier, division: player.rankDivision, lp: player.lp } : null}
              />
              <Rang
                label="Flex"
                rang={
                  player.flexRankTier
                    ? { tier: player.flexRankTier, division: player.flexRankDivision, lp: player.flexLp }
                    : null
                }
              />
              <Rang
                label="Peak"
                rang={player.peakTier ? { tier: player.peakTier, division: player.peakDivision, lp: player.peakLp } : null}
                complement={player.peakSeason}
              />
              {role && (
                <span className="flex items-center gap-1">
                  <RoleIcon role={player.mainRole} />
                  {role}
                </span>
              )}
            </div>
            {fiche && (
              <div role="group" aria-label="Titres" className="flex flex-wrap gap-2">
                <Badge className="bg-yellow-400 text-black">{fiche.titres.mvp} MVP</Badge>
                <Badge variant="destructive">{fiche.titres.dofd} DOFD</Badge>
              </div>
            )}
          </div>
        </div>
        <div className="flex items-center gap-3">
          <CostTierBadge cost={player.currentCost} className="px-4 py-1 text-5xl" />
          <span className="text-sm text-muted-foreground">{points(player.currentCost)}</span>
        </div>
      </CardContent>
    </Card>
  );
}

/** Un rang ; `null` quand le joueur n'en a pas (affiché comme non renseigné, jamais comme un rang vide). */
function Rang({
  label,
  rang,
  complement,
}: {
  label: string;
  rang: { tier: string; division: number | null; lp: number | null } | null;
  complement?: string | null;
}) {
  return (
    <span className="flex items-center gap-1.5">
      {label}
      {rang ? <RankBadge tier={rang.tier} division={rang.division} lp={rang.lp} /> : <span>non renseigné</span>}
      {rang && complement && <span>{complement}</span>}
    </span>
  );
}

function Section({ titre, children, className }: { titre: string; children: ReactNode; className?: string }) {
  return (
    <Card role="region" aria-label={titre} className={className}>
      <CardHeader>
        <CardTitle className="text-sm text-muted-foreground">{titre}</CardTitle>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}

function StatsEnCustoms({ stats }: { stats: Stats }) {
  const tuiles: Array<[string, string]> = [
    [pourcentLabel(stats.winrate), "winrate"],
    [decimalLabel(stats.kda), "KDA"],
    [decimalLabel(stats.csParMinute), "CS par minute"],
    [pourcentLabel(stats.participationAuxKills), "participation aux kills"],
    [entierLabel(stats.degatsParMinute), "dégâts par minute"],
    [entierLabel(stats.noteMoyenne), "Note moyenne"],
  ];
  return (
    <Section titre={`Stats en customs · ${stats.gamesJouees} games`}>
      <dl className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {tuiles.map(([valeur, libelle]) => (
          <div key={libelle} className="flex flex-col gap-1 rounded-lg border bg-muted/30 p-3">
            <dd className="order-1 font-mono text-xl font-semibold tabular-nums">{valeur}</dd>
            <dt className="order-2 text-xs text-muted-foreground">{libelle}</dt>
          </div>
        ))}
      </dl>
    </Section>
  );
}

function CoutMoisParMois({ playerId }: { playerId: number }) {
  const historique = $api.useQuery("get", "/players/{playerId}/cost/historique", { params: { path: { playerId } } });
  const courbe = historique.data ? pointsDeLaCourbe(historique.data.mois) : [];
  const description = `Cost mois par mois : ${courbe.map((p) => `${p.libelle} ${decimalLabel(p.cost, 2)}`).join(", ")}`;

  return (
    <Section titre="Cost, mois par mois">
      {historique.isPending ? (
        <Skeleton className="h-40 w-full" />
      ) : historique.isError ? (
        <ErrorAlert error={historique.error} />
      ) : courbe.length === 0 ? (
        <p className="text-sm text-muted-foreground">Pas encore d'historique de cost.</p>
      ) : (
        <svg
          role="img"
          aria-label={description}
          viewBox={`0 0 ${COURBE.largeur} ${COURBE.hauteur}`}
          className="h-auto w-full text-foreground"
        >
          {GRADUATIONS.map((g) => (
            <g key={g}>
              <line
                x1={COURBE.gauche}
                x2={COURBE.largeur - COURBE.droite}
                y1={yDuCost(g)}
                y2={yDuCost(g)}
                className="stroke-border"
                strokeWidth={1}
              />
              <text x={COURBE.gauche - 8} y={yDuCost(g)} textAnchor="end" dominantBaseline="middle" className="fill-muted-foreground text-[10px]">
                {g}
              </text>
            </g>
          ))}
          <polyline
            fill="none"
            className="stroke-current"
            strokeWidth={2}
            strokeLinejoin="round"
            points={courbe.map((p) => `${p.x},${p.y}`).join(" ")}
          />
          {courbe.map((p, i) => (
            <g key={p.mois + i}>
              <circle cx={p.x} cy={p.y} r={i === courbe.length - 1 ? 5 : 3.5} className="fill-current" />
              <text x={p.x} y={COURBE.hauteur - 8} textAnchor="middle" className="fill-muted-foreground text-[10px]">
                {p.mois}
              </text>
            </g>
          ))}
        </svg>
      )}
    </Section>
  );
}

function DernieresGames({ games }: { games: Fiche["games"] }) {
  const champions = useChampions();
  const catalogue = champions.data ?? [];
  return (
    <Section titre="Dernières games">
      {games.length === 0 ? (
        <p className="text-sm text-muted-foreground">Aucune game en customs.</p>
      ) : (
        <ul className="flex flex-col divide-y">
          {games.slice(0, 10).map((g) => {
            const champion = catalogue.find((c) => c.id === g.championId);
            const poste = posteLabel(g.role ?? g.stats?.position);
            return (
              <li key={g.gameId}>
                <Link
                  to="/games/$gameId"
                  params={{ gameId: String(g.gameId) }}
                  className="flex flex-wrap items-center gap-x-4 gap-y-1 py-2 text-sm hover:bg-muted/40"
                >
                  <span className="w-28 text-xs text-muted-foreground">{dateHeureLabel(g.playedAt)}</span>
                  <span className="flex min-w-0 flex-1 items-center gap-2">
                    <ChampionCarre nom={champion?.name ?? g.championId} image={champion?.imageUrl} />
                    <span className="flex min-w-0 flex-col">
                      <span className="truncate font-medium">{champion?.name ?? g.championId ?? "–"}</span>
                      {poste && <span className="text-xs text-muted-foreground">{poste}</span>}
                    </span>
                  </span>
                  <span className="font-mono tabular-nums">{kdaDetailLabel(g.stats)}</span>
                  <Badge variant={g.victoire ? "secondary" : "destructive"} className={cn(g.victoire && "text-emerald-400")}>
                    {g.victoire ? "Victoire" : "Défaite"}
                  </Badge>
                  <span className={cn("font-mono tabular-nums", g.performance && noteClass[noteTone(g.performance.note)])}>
                    <span className="font-sans text-xs text-muted-foreground">Note </span>
                    {g.performance ? `${g.performance.note}/100` : "–"}
                  </span>
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </Section>
  );
}

function ChampionCarre({ nom, image }: { nom: string | null; image?: string }) {
  return (
    <span className="flex size-8 shrink-0 items-center justify-center rounded-md border bg-muted text-xs font-semibold">
      {image ? <img src={image} alt="" className="size-full rounded-sm object-cover" /> : <span aria-hidden>{(nom ?? "?").charAt(0)}</span>}
    </span>
  );
}

function Barre({ value, max }: { value: number | null; max: number }) {
  if (value == null) return null;
  return (
    <progress
      aria-hidden
      value={value}
      max={max}
      className="h-1 w-full appearance-none overflow-hidden rounded-full bg-muted text-foreground [&::-moz-progress-bar]:bg-current [&::-webkit-progress-bar]:bg-muted [&::-webkit-progress-value]:bg-current"
    />
  );
}

function Champions({ champions }: { champions: Fiche["champions"] }) {
  const catalogue = useChampions().data ?? [];
  return (
    <Section titre="Champions">
      {champions.length === 0 ? (
        <p className="text-sm text-muted-foreground">Aucun champion joué.</p>
      ) : (
        <ul className="flex flex-col gap-3">
          {champions.map((c) => {
            const champion = catalogue.find((x) => x.id === c.championId);
            const nom = champion?.name ?? c.championId;
            return (
              <li key={c.championId} aria-label={nom} className="flex items-center gap-3">
                <ChampionCarre nom={nom} image={champion?.imageUrl} />
                <div className="flex min-w-0 flex-1 flex-col gap-1">
                  <div className="flex items-baseline justify-between gap-2 text-sm">
                    <span className="truncate font-medium">{nom}</span>
                    <span className="text-xs text-muted-foreground">
                      {c.stats.gamesJouees} games · {pourcentLabel(c.stats.winrate)}
                    </span>
                  </div>
                  <Barre value={c.stats.winrate} max={100} />
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </Section>
  );
}

function Roles({ roles }: { roles: Fiche["roles"] }) {
  const max = Math.max(1, ...roles.map((r) => r.stats.gamesJouees));
  return (
    <Section titre="Rôles">
      {roles.length === 0 ? (
        <p className="text-sm text-muted-foreground">Aucun rôle connu.</p>
      ) : (
        <ul className="flex flex-col gap-3">
          {roles.map((r) => {
            const nom = posteLabel(r.role) ?? "Rôle inconnu";
            return (
              <li key={nom} aria-label={nom} className="flex flex-col gap-1">
                <div className="flex items-baseline justify-between text-sm">
                  <span className="font-medium">{nom}</span>
                  <span className="text-xs text-muted-foreground">{r.stats.gamesJouees} games</span>
                </div>
                <Barre value={r.stats.gamesJouees} max={max} />
              </li>
            );
          })}
        </ul>
      )}
    </Section>
  );
}
