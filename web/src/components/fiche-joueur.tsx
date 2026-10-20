import { LigneDeGame } from "@/components/ligne-de-game";
import { keepPreviousData } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { type ReactNode, useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { AvatarJoueur } from "@/components/avatar-joueur";
import { ErrorAlert } from "@/components/query-state";
import { CostTierBadge, RankBadge, RoleIcon } from "@/components/rank";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { points } from "@/lib/cost";
import { useChampions } from "@/lib/draft-champions-queries";
import {
  COST_MAX,
  COST_MIN,
  COURBE,
  decimalLabel,
  entierLabel,
  pointsDeLaCourbe,
  pourcentLabel,
  yDuCost,
} from "@/lib/fiche";
import { posteLabel } from "@/lib/game";
import { roleLabel } from "@/lib/ranks";
import { cn } from "@/lib/utils";

type Fiche = Schemas["FicheJoueur"];
type Stats = Schemas["StatsDeCustoms"];

const GRADUATIONS = Array.from({ length: COST_MAX - COST_MIN + 1 }, (_, i) => COST_MIN + i);


type Filtres = {
  role: Schemas["Role"];
  regroupement: Schemas["Regroupement"];
  serieId: number | undefined;
  champion: string | undefined;
  resultat: Schemas["ResultatFiltre"];
};

const FILTRES_PAR_DEFAUT: Filtres = {
  role: null,
  regroupement: "Soiree",
  serieId: undefined,
  champion: undefined,
  resultat: null,
};

const winrateClass = (winrate: number | null) =>
  winrate == null ? "text-muted-foreground" : winrate >= 55 ? "text-emerald-400" : winrate < 45 ? "text-red-400" : "text-foreground";

/** La fiche publique : en-tête, colonnes, résumé, filtres et liste de games, tirés d'une seule fiche filtrée. */
export function FichePublique({ player }: { player: Schemas["PlayerDto"] }) {
  const [filtres, setFiltres] = useState<Filtres>(FILTRES_PAR_DEFAUT);
  const fiche = $api.useQuery(
    "get",
    "/players/{playerId}/fiche",
    {
      params: {
        path: { playerId: player.id },
        query: {
          regroupement: filtres.regroupement,
          role: filtres.role ?? undefined,
          serieId: filtres.serieId,
          champion: filtres.champion,
          resultat: filtres.resultat ?? undefined,
        },
      },
    },
    { placeholderData: keepPreviousData },
  );

  return (
    <>
      <HeroDuJoueur player={player} fiche={fiche.data} />
      {fiche.isPending ? (
        <>
          <RangDuJoueur player={player} />
          <Skeleton className="h-64 w-full" />
        </>
      ) : fiche.isError ? (
        <>
          <RangDuJoueur player={player} />
          <ErrorAlert error={fiche.error} />
        </>
      ) : (
        <div className="flex flex-wrap items-start gap-4">
          <div className="flex min-w-0 flex-1 basis-80 flex-col gap-4 lg:max-w-sm">
            <RangDuJoueur player={player} />
            <Section titre="Cost, mois par mois">
              <CourbeDuCost playerId={player.id} />
            </Section>
            <PerformanceParChampion champions={fiche.data.champions} />
            <PerformanceParRole roles={fiche.data.roles} />
            <JoueAvec coequipiers={fiche.data.joueAvec} />
            <StatsEnCustoms stats={fiche.data.stats} />
          </div>
          <div className="flex min-w-0 flex-[999_1_34rem] flex-col gap-4">
            <ResumeDesDernieresGames resume={fiche.data.resume} />
            <BarreDeFiltres filtres={filtres} onChange={setFiltres} fiche={fiche.data} />
            <ListeDesGames groupes={fiche.data.liste} regroupement={filtres.regroupement} />
          </div>
        </div>
      )}
    </>
  );
}

/** Identité, rôle principal, Discord, titres et tier de cost en grand. */
export function HeroDuJoueur({ player, fiche }: { player: Schemas["PlayerDto"]; fiche?: Fiche }) {
  const role = roleLabel(player.mainRole);

  return (
    <Card>
      <CardContent className="flex flex-wrap items-center justify-between gap-6">
        <div className="flex min-w-0 items-center gap-4">
          <AvatarJoueur prenom={player.prenom} avatarUrl={player.avatarUrl} className="size-14 text-xl" />
          <div className="flex min-w-0 flex-col gap-2">
            <div>
              <h1 className="text-2xl font-bold">{player.prenom}</h1>
              <p className="text-sm text-muted-foreground">
                <span>{player.riotId}</span>
                {fiche?.discord && <span> · Discord lié</span>}
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
              {role && (
                <span className="flex items-center gap-1">
                  <RoleIcon role={player.mainRole} />
                  <span>{role}</span> <span>principal</span>
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

/** Le rang Solo avec le peak déclaré et le rang Flex. */
function RangDuJoueur({ player }: { player: Schemas["PlayerDto"] }) {
  return (
    <Section titre="Classé Solo">
      <div className="flex flex-col gap-3 text-sm">
        <Rang
          label="SoloQ"
          rang={player.rankTier ? { tier: player.rankTier, division: player.rankDivision, lp: player.lp } : null}
        />
        <div className="flex flex-col gap-2 border-t pt-3 text-xs text-muted-foreground">
          <Rang
            label="Peak"
            rang={player.peakTier ? { tier: player.peakTier, division: player.peakDivision, lp: player.peakLp } : null}
            complement={player.peakSeason}
          />
          <Rang
            label="Flex"
            rang={
              player.flexRankTier
                ? { tier: player.flexRankTier, division: player.flexRankDivision, lp: player.flexLp }
                : null
            }
          />
        </div>
      </div>
    </Section>
  );
}

const NB_CHAMPIONS_VISIBLES = 3;

function PerformanceParChampion({ champions }: { champions: Fiche["champions"] }) {
  const catalogue = useChampions().data ?? [];
  const [tous, setTous] = useState(false);
  const visibles = tous ? champions : champions.slice(0, NB_CHAMPIONS_VISIBLES);
  return (
    <Section titre="Performance par champion">
      {champions.length === 0 ? (
        <p className="text-sm text-muted-foreground">Aucun champion joué.</p>
      ) : (
        <div className="flex flex-col gap-3">
          <ul className="flex flex-col gap-3">
            {visibles.map((c) => {
              const champion = catalogue.find((x) => x.id === c.championId);
              const nom = champion?.name ?? c.championId;
              return (
                <li key={c.championId} aria-label={nom} className="flex items-center gap-3 text-sm">
                  <ChampionCarre nom={nom} image={champion?.imageUrl} />
                  <span className="flex min-w-0 flex-1 flex-col leading-tight">
                    <span className="truncate font-medium">{nom}</span>
                    <span className="text-xs text-muted-foreground">
                      {decimalLabel(c.stats.kda)} KDA ·{" "}
                      {[c.stats.killsMoyens, c.stats.mortsMoyennes, c.stats.assistsMoyens].map((v) => decimalLabel(v)).join(" / ")}
                    </span>
                  </span>
                  <span className="w-14 text-right text-xs text-muted-foreground">{decimalLabel(c.stats.csParMinute)} CS/m</span>
                  <span className="w-16 text-right text-xs text-muted-foreground">{c.stats.gamesJouees} games</span>
                  <span className={cn("w-10 text-right font-medium", winrateClass(c.stats.winrate))}>
                    {pourcentLabel(c.stats.winrate)}
                  </span>
                </li>
              );
            })}
          </ul>
          {champions.length > NB_CHAMPIONS_VISIBLES && !tous && (
            <Button variant="outline" size="sm" onClick={() => setTous(true)}>
              Tous les champions
            </Button>
          )}
        </div>
      )}
    </Section>
  );
}

function PerformanceParRole({ roles }: { roles: Fiche["roles"] }) {
  return (
    <Section titre="Performance par rôle">
      {roles.length === 0 ? (
        <p className="text-sm text-muted-foreground">Aucun rôle connu.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {roles.map((r) => {
            const nom = posteLabel(r.role) ?? "Rôle inconnu";
            return (
              <li key={nom} aria-label={nom} className="flex items-center justify-between text-sm">
                <span className="w-20 font-medium">{nom}</span>
                <span className="text-xs text-muted-foreground">{r.stats.gamesJouees} games</span>
                <span className={cn("font-medium", winrateClass(r.stats.winrate))}>{pourcentLabel(r.stats.winrate)}</span>
              </li>
            );
          })}
        </ul>
      )}
    </Section>
  );
}

function JoueAvec({ coequipiers }: { coequipiers: Fiche["joueAvec"] }) {
  return (
    <Section titre="Joué avec">
      {coequipiers.length === 0 ? (
        <p className="text-sm text-muted-foreground">Personne pour l'instant.</p>
      ) : (
        <ul className="flex flex-col gap-3">
          {coequipiers.map((c) => (
            <li key={c.playerId} className="flex items-center gap-3 text-sm">
              <AvatarJoueur prenom={c.prenom} avatarUrl={null} className="size-8 text-xs" />
              <span className="flex min-w-0 flex-1 flex-col leading-tight">
                <Link to="/joueurs/$playerId" params={{ playerId: String(c.playerId) }} className="truncate font-medium hover:underline">
                  {c.prenom}
                </Link>
                <span className="text-xs text-muted-foreground">{c.parties} games</span>
              </span>
              <span className="flex flex-col items-end leading-tight">
                <span className={cn("font-medium", winrateClass(c.winrate))}>{pourcentLabel(c.winrate)} WR</span>
                <span className="text-xs text-muted-foreground">
                  {c.victoires} V – {c.defaites} D
                </span>
              </span>
            </li>
          ))}
        </ul>
      )}
    </Section>
  );
}

/** Une jauge en demi-cercle : la part de victoires en bleu, le reste en rouge. */
function JaugeDeWinrate({ winrate }: { winrate: number | null }) {
  const arc = "M12 76 A63 63 0 0 1 138 76";
  return (
    <svg width="150" height="84" viewBox="0 0 150 84" role="img" aria-label={winrate == null ? "Winrate inconnu" : `Winrate ${Math.round(winrate)} pour cent`}>
      <path d={arc} pathLength={100} fill="none" strokeWidth={12} className={winrate == null ? "stroke-muted" : "stroke-rose-600"} />
      {winrate != null && winrate > 0 && (
        <path d={arc} pathLength={100} fill="none" strokeWidth={12} strokeDasharray={`${winrate} 100`} className="stroke-blue-500" />
      )}
      <text x="75" y="64" textAnchor="middle" className="fill-foreground text-[22px] font-semibold">
        {pourcentLabel(winrate)}
      </text>
    </svg>
  );
}

function ResumeDesDernieresGames({ resume }: { resume: Fiche["resume"] }) {
  const catalogue = useChampions().data ?? [];
  const titres: Array<[string, number, string]> = [
    ["Meilleure perf", resume.meilleuresPerfs, "bg-sky-400/15 text-sky-300"],
    ["ACE", resume.aces, "bg-violet-400/15 text-violet-300"],
    ["MVP", resume.mvp, "bg-yellow-300/15 text-yellow-200"],
    ["DOFD", resume.dofd, "bg-red-400/15 text-red-300"],
  ];
  return (
    <Section titre="30 dernières games">
      <div className="flex flex-wrap items-center justify-between gap-6">
        <div className="flex flex-col items-center gap-1">
          <JaugeDeWinrate winrate={resume.winrate} />
          <span className="text-xs text-muted-foreground">
            {resume.victoires} V – {resume.defaites} D
          </span>
        </div>
        <ul className="flex flex-col gap-2 text-sm">
          {resume.topChampions.map((c) => {
            const champion = catalogue.find((x) => x.id === c.championId);
            const nom = champion?.name ?? c.championId;
            return (
              <li key={c.championId} aria-label={nom} className="flex items-center gap-2">
                <ChampionCarre nom={nom} image={champion?.imageUrl} />
                <span className="flex flex-col leading-tight">
                  <span>
                    <span className="font-medium">{pourcentLabel(c.winrate)}</span>{" "}
                    <span className="text-xs text-muted-foreground">{c.games} games</span>
                  </span>
                  <span className="text-xs text-muted-foreground">{decimalLabel(c.kda)} KDA</span>
                </span>
              </li>
            );
          })}
        </ul>
        <div className="flex flex-col items-center leading-tight">
          <span className="text-xs text-muted-foreground">KDA moyen</span>
          <span className="font-mono text-3xl font-semibold">{decimalLabel(resume.kda)}</span>
        </div>
        <div className="flex flex-col items-center leading-tight">
          <span className="text-xs text-muted-foreground">Note moyenne</span>
          <span className="text-2xl font-semibold">
            {entierLabel(resume.noteMoyenne)}
            {resume.noteMoyenne != null && <span className="text-sm text-muted-foreground">/100</span>}
          </span>
        </div>
        <div className="flex flex-wrap justify-center gap-3">
          {titres.map(([nom, nombre, classe]) => (
            <div key={nom} className="flex flex-col items-center gap-1">
              <span className={cn("rounded-md px-2 py-0.5 text-xs font-semibold", classe)}>{nom}</span>
              <span className="text-sm">× {nombre}</span>
            </div>
          ))}
        </div>
      </div>
    </Section>
  );
}

const ROLES_FILTRABLES: Array<[string, Schemas["Role"]]> = [
  ["Tous", null],
  ["Top", "Top"],
  ["Jungle", "Jungle"],
  ["Mid", "Mid"],
  ["ADC", "Adc"],
  ["Support", "Support"],
];

const REGROUPEMENTS: Array<[string, Schemas["Regroupement"]]> = [
  ["Soirée", "Soiree"],
  ["Jour", "Jour"],
  ["Aucun", "Plat"],
];

function Choix<T>({
  label,
  options,
  value,
  onChange,
}: {
  label: string;
  options: Array<[string, T]>;
  value: T;
  onChange: (value: T) => void;
}) {
  return (
    <div role="group" aria-label={label} className="flex gap-1 rounded-lg border bg-card p-1">
      {options.map(([nom, valeur]) => (
        <button
          key={nom}
          type="button"
          aria-pressed={valeur === value}
          onClick={() => onChange(valeur)}
          className={cn(
            "rounded-md px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground",
            valeur === value && "bg-white/10 font-medium text-foreground",
          )}
        >
          {nom}
        </button>
      ))}
    </div>
  );
}

const selectClass = "h-9 rounded-md border bg-card px-2 text-sm";

function BarreDeFiltres({
  filtres,
  onChange,
  fiche,
}: {
  filtres: Filtres;
  onChange: (filtres: Filtres) => void;
  fiche: Fiche;
}) {
  const catalogue = useChampions().data ?? [];
  const series = [...new Set(fiche.games.map((g) => g.seriesId).filter((id): id is number => id != null))];
  const maj = (partiel: Partial<Filtres>) => onChange({ ...filtres, ...partiel });
  return (
    <div role="group" aria-label="Filtres de la liste" className="flex flex-wrap items-center gap-3">
      <Choix label="Rôle" options={ROLES_FILTRABLES} value={filtres.role} onChange={(role) => maj({ role })} />
      <Choix
        label="Regroupement"
        options={REGROUPEMENTS}
        value={filtres.regroupement}
        onChange={(regroupement) => maj({ regroupement })}
      />
      <label className="flex items-center gap-2 text-sm text-muted-foreground">
        Série
        <select
          className={selectClass}
          value={filtres.serieId ?? ""}
          onChange={(e) => maj({ serieId: e.target.value ? Number(e.target.value) : undefined })}
        >
          <option value="">Toutes</option>
          {series.map((id) => (
            <option key={id} value={id}>
              Série {id}
            </option>
          ))}
        </select>
      </label>
      <label className="flex items-center gap-2 text-sm text-muted-foreground">
        Champion
        <select
          className={selectClass}
          value={filtres.champion ?? ""}
          onChange={(e) => maj({ champion: e.target.value || undefined })}
        >
          <option value="">Tous</option>
          {fiche.champions.map((c) => (
            <option key={c.championId} value={c.championId}>
              {catalogue.find((x) => x.id === c.championId)?.name ?? c.championId}
            </option>
          ))}
        </select>
      </label>
      <label className="flex items-center gap-2 text-sm text-muted-foreground">
        Résultat
        <select
          className={selectClass}
          value={filtres.resultat ?? ""}
          onChange={(e) => maj({ resultat: (e.target.value || null) as Filtres["resultat"] })}
        >
          <option value="">Tous</option>
          <option value="Victoire">Victoires</option>
          <option value="Defaite">Défaites</option>
        </select>
      </label>
      <Button variant="outline" size="sm" className="ml-auto" onClick={() => onChange(FILTRES_PAR_DEFAUT)}>
        Réinitialiser
      </Button>
    </div>
  );
}

const jourCourt = (iso: string) => new Date(iso).toLocaleDateString("fr-FR", { day: "numeric", month: "short" });
const pluriel = (n: number, mot: string) => `${n} ${mot}${n > 1 ? "s" : ""}`;

function titreDuGroupe(groupe: Fiche["liste"][number], regroupement: Schemas["Regroupement"]): string | null {
  const premiere = groupe.games[0];
  if (regroupement === "Soiree") {
    return groupe.soireeId != null && premiere ? `Soirée du ${jourCourt(premiere.playedAt)}` : "Hors soirée";
  }
  if (regroupement === "Jour" && groupe.jour) return jourCourt(`${groupe.jour}T12:00:00Z`);
  return null;
}

function ListeDesGames({
  groupes,
  regroupement,
}: {
  groupes: Fiche["liste"];
  regroupement: Schemas["Regroupement"];
}) {
  if (groupes.length === 0) return <p className="text-sm text-muted-foreground">Aucune game.</p>;
  return (
    <div className="flex flex-col gap-4">
      {groupes.map((groupe, i) => {
        const titre = titreDuGroupe(groupe, regroupement);
        const notes = groupe.games.flatMap((g) => (g.performance ? [g.performance.note] : []));
        const victoires = groupe.games.filter((g) => g.victoire).length;
        return (
          <section key={`${groupe.soireeId}-${groupe.jour}-${i}`} role="group" aria-label={titre ?? "Games"} className="flex flex-col gap-2">
            {titre && (
              <div className="flex flex-wrap items-baseline justify-between gap-2 px-0.5">
                <h2 className="text-base font-semibold">{titre}</h2>
                <span className="text-xs text-muted-foreground">
                  Note moy. {notes.length ? Math.round(notes.reduce((a, b) => a + b, 0) / notes.length) : "–"} ·{" "}
                  {pluriel(victoires, "victoire")} · {pluriel(groupe.games.length - victoires, "défaite")}
                </span>
              </div>
            )}
            <ul className="flex flex-col gap-2">
              {groupe.games.map((g) => (
                <li key={g.gameId}>
                  <LigneDeGame game={g} />
                </li>
              ))}
            </ul>
          </section>
        );
      })}
    </div>
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

/** La courbe du cost mois par mois d'un Joueur (réutilisée par le Profil). */
export function CourbeDuCost({ playerId }: { playerId: number }) {
  const historique = $api.useQuery("get", "/players/{playerId}/cost/historique", { params: { path: { playerId } } });
  const courbe = historique.data ? pointsDeLaCourbe(historique.data.mois) : [];
  const description = `Cost mois par mois : ${courbe.map((p) => `${p.libelle} ${decimalLabel(p.cost, 2)}`).join(", ")}`;

  return (
    <>
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
    </>
  );
}

function ChampionCarre({ nom, image }: { nom: string | null; image?: string }) {
  return (
    <span className="flex size-8 shrink-0 items-center justify-center rounded-md border bg-muted text-xs font-semibold">
      {image ? <img src={image} alt="" className="size-full rounded-sm object-cover" /> : <span aria-hidden>{(nom ?? "?").charAt(0)}</span>}
    </span>
  );
}

