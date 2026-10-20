import { Link } from "@tanstack/react-router";
import { useRef, useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { AvatarJoueur } from "@/components/avatar-joueur";
import { type Participant, type Side, sideName } from "@/components/game-equipe-table";
import { AnneauDeNote } from "@/components/icones-de-jeu/anneau-de-note";
import { BadgeDeTitre, type Titre } from "@/components/icones-de-jeu/badge-de-titre";
import { IconeObjectif, type Objectif } from "@/components/icones-de-jeu/icone-objectif";
import { IconeRole, type Role } from "@/components/icones-de-jeu/icone-role";
import { IconeChampion, IconeObjet, IconeRune, IconeSort } from "@/components/icones-de-jeu/icones-catalogue";
import { PanneauImpact } from "@/components/panneau-impact";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { millierLabel, nombreLabel, parMinuteLabel, partLabel, statLabel, titreDeBadge, trierParRole } from "@/lib/game";
import { rankLabel } from "@/lib/ranks";
import { sideBorderClass, sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";

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

/** Le Roi ou la Reine (vote MVP) et le DOFD de la Game, d'après les votes clos (aucun tant qu'ils ne le sont pas). */
function useTitres(gameId: number) {
  const votes = $api.useQuery("get", "/games/{id}/votes", { params: { path: { id: gameId } } });
  return {
    mvp: votes.data?.mvp.gagnants ?? [],
    dofd: votes.data?.dofd.gagnants ?? [],
  };
}

type Objectifs = Schemas["ObjectifsDEquipe"];

const OBJECTIFS: Array<[Objectif, (o: Objectifs) => number | null]> = [
  ["tourelle", (o) => o.towerKills],
  ["dragon", (o) => o.dragonKills],
  ["baron", (o) => o.baronKills],
  ["heraut", (o) => o.riftHeraldKills],
  ["larve", (o) => o.hordeKills],
  ["inhibiteur", (o) => o.inhibitorKills],
];

/** Les objectifs de l'équipe en icônes avec leur compteur ; rien quand le Rapport ne les donne pas. */
function ObjectifsEnIcones({ objectifs, side }: { objectifs: Objectifs | null; side: Side }) {
  if (!objectifs) return null;
  return (
    <div className="flex flex-wrap items-center gap-x-5 gap-y-1">
      {OBJECTIFS.map(([objectif, lire]) => (
        <span key={objectif} className="flex items-center gap-1.5">
          <IconeObjectif objectif={objectif} equipe={side === "blue" ? 100 : 200} className="size-6" />
          <span className="font-mono text-lg font-semibold tabular-nums">{statLabel(lire(objectifs))}</span>
        </span>
      ))}
    </div>
  );
}

/** « 1er », « 2e »… : la place du joueur dans la game d'après sa note. */
const placeLabel = (place: number): string => (place === 1 ? "1er" : `${place}e`);

const ROLES: Record<string, Role> = { Top: "Top", Jungle: "Jungle", Mid: "Mid", Adc: "Adc", Support: "Support" };

/**
 * L'équipe façon client de LoL, une ligne par joueur : rôle, place et badge, champion, nom (seul lien vers sa fiche ; le reste de la ligne ouvre son Impact),
 * sorts et runes, objets, K/D/A, participation aux kills, CS/min (vision pour le Support), dégâts et note.
 */
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
  const [ouvert, setOuvert] = useState<number | null>(null);
  const ligneOuverte = useRef<HTMLElement | null>(null);
  const maxDegats = Math.max(0, ...sidesOf(game).flatMap((s) => s.players.map((p) => p.stats?.damageToChampions ?? 0)));
  const joueurOuvert = players.find((p) => p.playerId === ouvert);
  const label = `Équipe ${team} · ${sideName(side)} side`;
  const objectifs = team === "A" ? game.teamAObjectifs : game.teamBObjectifs;

  return (
    <section className="flex flex-col gap-2 rounded-xl border bg-card p-4">
      <header className="flex flex-wrap items-center gap-x-6 gap-y-2">
        <div className="flex items-baseline gap-2">
          <h2 className={cn("text-xl font-semibold", sideTextClass(side))}>
            {game.winner === team ? "Victoire" : "Défaite"}
          </h2>
          <span className="text-sm text-muted-foreground">{label}</span>
        </div>
        <ObjectifsEnIcones objectifs={objectifs} side={side} />
      </header>
      <div className="overflow-x-auto">
        <ul aria-label={label} className="flex min-w-[64rem] flex-col gap-1">
          {players.map((p) => {
            const d = p.details;
            const s = p.stats;
            const role = ROLES[d?.role ?? s?.position ?? ""];
            const badge: Titre | null = titreDeBadge(p.badge);
            const estDofd = titres.dofd.includes(p.playerId);
            const estRoi = titres.mvp.includes(p.playerId);
            const degats = s?.damageToChampions ?? null;
            const meilleur = degats != null && degats > 0 && degats === maxDegats;
            const fiche = { to: "/joueurs/$playerId", params: { playerId: String(p.playerId) } } as const;
            return (
              <li
                key={p.playerId}
                aria-label={p.prenom}
                {...(p.impact && {
                  tabIndex: 0,
                  "aria-haspopup": "dialog" as const,
                  onClick: (e: React.MouseEvent) => {
                    if ((e.target as HTMLElement).closest("a")) return;
                    ligneOuverte.current = e.currentTarget as HTMLElement;
                    setOuvert(p.playerId);
                  },
                  onKeyDown: (e: React.KeyboardEvent) => {
                    if (e.target !== e.currentTarget || (e.key !== "Enter" && e.key !== " ")) return;
                    e.preventDefault();
                    ligneOuverte.current = e.currentTarget as HTMLElement;
                    setOuvert(p.playerId);
                  },
                })}
                className={cn(
                  "grid grid-cols-[2rem_4.5rem_minmax(12rem,1.4fr)_5rem_13rem_6rem_4.5rem_4.5rem_9rem_3.5rem] items-center gap-x-2 rounded-xl border border-transparent bg-muted/20 px-2.5 py-2 focus-visible:border-foreground/70 focus-visible:outline-none",
                  p.impact && "cursor-pointer hover:border-foreground/70 hover:bg-muted/40",
                )}
              >
                <div className="flex justify-center">
                  {role && <IconeRole role={role} className="size-6 text-foreground/80" />}
                </div>

                <div className="flex flex-col items-center gap-0.5 text-center">
                  <span className="text-xs font-semibold whitespace-nowrap text-muted-foreground">
                    {p.place == null ? "–" : placeLabel(p.place)}
                  </span>
                  {badge && <BadgeDeTitre titre={badge} />}
                </div>

                <div className="flex min-w-0 items-center gap-2.5">
                  <span className="relative shrink-0">
                    <IconeChampion
                      championId={p.championId}
                      className={cn("size-12 rounded-lg border-2", sideBorderClass(side))}
                    />
                    {s?.champLevel != null && (
                      <span className="absolute -right-1 -bottom-1 flex h-5 min-w-5 items-center justify-center rounded-full border bg-background px-1 font-mono text-[11px] font-semibold tabular-nums">
                        {s.champLevel}
                      </span>
                    )}
                  </span>
                  <div className="flex min-w-0 flex-col leading-tight">
                    <span className="flex flex-wrap items-center gap-1.5">
                      <Link {...fiche} className="truncate font-semibold hover:underline">
                        {p.prenom}
                      </Link>
                      {estDofd && <BadgeDeTitre titre="DOFD" />}
                      {estRoi && <BadgeDeTitre titre="Roi/Reine" />}
                    </span>
                    <span className="truncate text-xs text-muted-foreground">
                      {rankLabel(p.rankTier, p.rankDivision, null)}
                    </span>
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-1">
                  <IconeSort sortId={d?.spell1Id ?? s?.spell1Id} className="size-8" />
                  <IconeRune runeId={d?.perks?.[0]} className="size-8 rounded-full" />
                  <IconeSort sortId={d?.spell2Id ?? s?.spell2Id} className="size-8" />
                  <IconeRune runeId={d?.perkSubStyle} className="size-8 rounded-full" />
                </div>

                <Objets items={d?.items ?? s?.items ?? null} roleItem={d?.objetDeRole ?? null} />

                <div className="flex items-baseline justify-center gap-1 font-mono text-lg font-medium tabular-nums">
                  <span>{statLabel(s?.kills ?? null)}</span>
                  <span className="text-muted-foreground">/</span>
                  <span className="text-red-300">{statLabel(s?.deaths ?? null)}</span>
                  <span className="text-muted-foreground">/</span>
                  <span>{statLabel(s?.assists ?? null)}</span>
                </div>

                <Mesure
                  valeur={d?.partDesKills == null ? "–" : partLabel(d.partDesKills, 1)}
                  legende="KP"
                />
                {role === "Support" ? (
                  <Mesure valeur={statLabel(s?.visionScore ?? null)} legende="vision" />
                ) : (
                  <Mesure valeur={nombreLabel(d?.csParMinute ?? null, 1)} legende="CS/min" />
                )}

                <div className="flex flex-col gap-1">
                  <progress
                    aria-hidden
                    data-meilleur={String(meilleur)}
                    value={degats ?? 0}
                    max={maxDegats || 1}
                    className={cn(
                      "h-2 w-full appearance-none overflow-hidden rounded-full bg-muted [&::-moz-progress-bar]:bg-emerald-700 [&::-webkit-progress-bar]:bg-muted [&::-webkit-progress-value]:bg-emerald-700",
                      meilleur &&
                        "shadow-[0_0_10px_rgb(187_247_208/0.6)] [&::-moz-progress-bar]:bg-emerald-200 [&::-webkit-progress-value]:bg-emerald-200",
                    )}
                  />
                  <span className="font-mono text-xs tabular-nums text-muted-foreground">
                    {millierLabel(degats)} ({parMinuteLabel(d?.degatsParMinute ?? null)})
                  </span>
                </div>

                <div className="flex justify-center">
                  {p.performance ? (
                    <AnneauDeNote note={p.performance.note} className="size-12" />
                  ) : (
                    <span className="text-muted-foreground">–</span>
                  )}
                </div>
              </li>
            );
          })}
        </ul>
      </div>
      {joueurOuvert?.impact && (
        <PanneauImpact
          joueur={joueurOuvert}
          impact={joueurOuvert.impact}
          adversaire={
            sidesOf(game)
              .flatMap((s) => s.players)
              .find((j) => j.playerId === joueurOuvert.impact?.faceAFace?.adversaireId) ?? null
          }
          equipe={team}
          victoire={game.winner === team}
          onFermer={() => setOuvert(null)}
          retour={ligneOuverte}
        />
      )}
    </section>
  );
}

function Mesure({ valeur, legende }: { valeur: string; legende: string }) {
  return (
    <div className="flex flex-col items-center leading-tight">
      <span className="font-mono text-base font-semibold tabular-nums">{valeur}</span>
      <span className="text-[10px] text-muted-foreground">{legende}</span>
    </div>
  );
}

/** Six objets, la babiole ronde, puis l'objet de rôle sans texte : doré terminé, grisé en cours, vide sans quête. */
function Objets({
  items,
  roleItem,
}: {
  items: Array<number | null> | null;
  roleItem: Schemas["ObjetDeRole"] | null;
}) {
  const emplacements = Array.from({ length: 7 }, (_, i) => items?.[i] ?? null);
  const fini = roleItem?.etat === "Terminee";
  return (
    <div className="flex items-center gap-1.5">
      <ul aria-label="Objets" className="grid grid-cols-[repeat(3,1.75rem)_1.75rem] items-center gap-0.5">
        {emplacements.slice(0, 6).map((id, i) => (
          <li key={i}>
            <IconeObjet objetId={id} className="size-7" />
          </li>
        ))}
        <li className="col-start-4 row-span-2 row-start-1 self-center">
          <IconeObjet objetId={emplacements[6]} className="size-7 rounded-full" />
        </li>
      </ul>
      {roleItem ? (
        <div
          role="group"
          aria-label={fini ? "Objet de rôle terminé" : "Objet de rôle en cours"}
          className={cn("rounded-lg", fini ? "ring-2 ring-amber-400" : "opacity-50 grayscale")}
        >
          <IconeObjet objetId={roleItem.id} className="size-8" />
        </div>
      ) : (
        <span aria-hidden className="size-8 shrink-0 rounded-lg border border-dashed border-border" />
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
