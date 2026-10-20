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

/** Le DOFD de la Game, d'après les votes clos (aucun tant qu'ils ne le sont pas). */
function useTitres(gameId: number) {
  const votes = $api.useQuery("get", "/games/{id}/votes", { params: { path: { id: gameId } } });
  return {
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
function ObjectifsEnIcones({
  objectifs,
  side,
  compact,
}: {
  objectifs: Objectifs | null;
  side: Side;
  compact: boolean;
}) {
  if (!objectifs) return null;
  return (
    <div className={cn("flex flex-wrap items-center gap-y-1", compact ? "gap-x-3" : "gap-x-5")}>
      {OBJECTIFS.map(([objectif, lire]) => (
        <span key={objectif} className="flex items-center gap-1.5">
          <IconeObjectif objectif={objectif} equipe={side === "blue" ? 100 : 200} className={compact ? "size-4" : "size-6"} />
          <span className={cn("font-mono font-semibold tabular-nums", compact ? "text-sm" : "text-lg")}>{statLabel(lire(objectifs))}</span>
        </span>
      ))}
    </div>
  );
}

/** « 1er », « 2e »… : la place du joueur dans la game d'après sa note. */
const placeLabel = (place: number): string => (place === 1 ? "1er" : `${place}e`);

/** Les dimensions d'une ligne de joueur : la densité normale de la page d'une game, ou la compacte du panneau déplié d'une ligne de game. */
const DENSITES = {
  normale: {
    colonnes:
      "grid-cols-[2rem_4.5rem_minmax(12rem,1.4fr)_5rem_13rem_6rem_4.5rem_4.5rem_9rem_3.5rem] gap-x-2 px-2.5 py-2",
    largeurMin: "min-w-[64rem]",
    role: "size-6",
    portrait: "size-12 rounded-lg border-2",
    niveau: "h-5 min-w-5 px-1 text-[11px]",
    nom: "gap-2.5",
    sousTexte: "text-xs",
    sorts: "size-8",
    kda: "text-lg",
    mesure: "text-base",
    legende: "text-[10px]",
    barre: "h-2",
    degats: "text-xs",
    note: "size-12",
  },
  compacte: {
    colonnes:
      "grid-cols-[1.25rem_2.25rem_minmax(7rem,1.4fr)_2.75rem_8rem_4.25rem_2.5rem_2.5rem_5.5rem_2rem] gap-x-1.5 px-1.5 py-1",
    largeurMin: "min-w-[40rem]",
    role: "size-4",
    portrait: "size-9 rounded-md border",
    niveau: "h-3.5 min-w-3.5 px-0.5 text-[9px]",
    nom: "gap-1.5",
    sousTexte: "text-[10px]",
    sorts: "size-[1.125rem]",
    kda: "text-sm",
    mesure: "text-xs",
    legende: "text-[8px]",
    barre: "h-1",
    degats: "text-[10px]",
    note: "size-8",
  },
} as const;

type Densite = (typeof DENSITES)[keyof typeof DENSITES];

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
  compact = false,
}: {
  game: Schemas["GameRecapResponse"];
  team: string;
  side: Side;
  players: Participant[];
  /** Densité réduite, pour le panneau déplié d'une ligne de game. */
  compact?: boolean;
}) {
  const dim: Densite = DENSITES[compact ? "compacte" : "normale"];
  const titres = useTitres(game.id);
  const [ouvert, setOuvert] = useState<number | null>(null);
  const ligneOuverte = useRef<HTMLElement | null>(null);
  const maxDegats = Math.max(0, ...sidesOf(game).flatMap((s) => s.players.map((p) => p.stats?.damageToChampions ?? 0)));
  const joueurOuvert = players.find((p) => p.playerId === ouvert);
  const label = `Équipe ${team} · ${sideName(side)} side`;
  const objectifs = team === "A" ? game.teamAObjectifs : game.teamBObjectifs;

  return (
    <section className={cn("flex flex-col rounded-xl border bg-card", compact ? "gap-1 p-2" : "gap-2 p-4")}>
      <header className={cn("flex flex-wrap items-center gap-y-1", compact ? "gap-x-4" : "gap-x-6 gap-y-2")}>
        <div className="flex items-baseline gap-2">
          <h2 className={cn("font-semibold", compact ? "text-base" : "text-xl", sideTextClass(side))}>
            {game.winner === team ? "Victoire" : "Défaite"}
          </h2>
          <span className={cn("text-muted-foreground", compact ? "text-xs" : "text-sm")}>{label}</span>
        </div>
        <ObjectifsEnIcones objectifs={objectifs} side={side} compact={compact} />
      </header>
      <div className="overflow-x-auto">
        <ul aria-label={label} className={cn("flex flex-col gap-1", dim.largeurMin)}>
          {players.map((p) => {
            const d = p.details;
            const s = p.stats;
            const role = ROLES[d?.role ?? s?.position ?? ""];
            const badge: Titre | null = titreDeBadge(p.badge);
            const estDofd = titres.dofd.includes(p.playerId);
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
                  "grid items-center rounded-xl border border-transparent bg-muted/20 focus-visible:border-foreground/70 focus-visible:outline-none",
                  dim.colonnes,
                  p.impact && "cursor-pointer hover:border-foreground/70 hover:bg-muted/40",
                )}
              >
                <div className="flex justify-center">
                  {role && <IconeRole role={role} className={cn("text-foreground/80", dim.role)} />}
                </div>

                <div className="flex flex-col items-center gap-0.5 text-center">
                  <span className={cn("font-semibold whitespace-nowrap text-muted-foreground", compact ? "text-[10px]" : "text-xs")}>
                    {p.place == null ? "–" : placeLabel(p.place)}
                  </span>
                  {badge && <BadgeDeTitre titre={badge} />}
                </div>

                <div className={cn("flex min-w-0 items-center", dim.nom)}>
                  <span className="relative shrink-0">
                    <IconeChampion
                      championId={p.championId}
                      className={cn(dim.portrait, sideBorderClass(side))}
                    />
                    {s?.champLevel != null && (
                      <span
                        className={cn(
                          "absolute -right-1 -bottom-1 flex items-center justify-center rounded-full border bg-background font-mono font-semibold tabular-nums",
                          dim.niveau,
                        )}
                      >
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
                    </span>
                    <span className={cn("truncate text-muted-foreground", dim.sousTexte)}>
                      {rankLabel(p.rankTier, p.rankDivision, null)}
                    </span>
                  </div>
                </div>

                <div className={cn("grid grid-cols-2", compact ? "gap-0.5" : "gap-1")}>
                  <IconeSort sortId={d?.spell1Id ?? s?.spell1Id} className={dim.sorts} />
                  <IconeRune runeId={d?.perks?.[0]} className={cn(dim.sorts, "rounded-full")} />
                  <IconeSort sortId={d?.spell2Id ?? s?.spell2Id} className={dim.sorts} />
                  <IconeRune runeId={d?.perkSubStyle} className={cn(dim.sorts, "rounded-full")} />
                </div>

                <Objets items={d?.items ?? s?.items ?? null} roleItem={d?.objetDeRole ?? null} compact={compact} />

                <div className={cn("flex items-baseline justify-center gap-1 font-mono font-medium tabular-nums", dim.kda)}>
                  <span>{statLabel(s?.kills ?? null)}</span>
                  <span className="text-muted-foreground">/</span>
                  <span className="text-red-300">{statLabel(s?.deaths ?? null)}</span>
                  <span className="text-muted-foreground">/</span>
                  <span>{statLabel(s?.assists ?? null)}</span>
                </div>

                <Mesure
                  valeur={d?.partDesKills == null ? "–" : partLabel(d.partDesKills, 1)}
                  legende="KP"
                  dim={dim}
                />
                {role === "Support" ? (
                  <Mesure valeur={statLabel(s?.visionScore ?? null)} legende="vision" dim={dim} />
                ) : (
                  <Mesure valeur={nombreLabel(d?.csParMinute ?? null, 1)} legende="CS/min" dim={dim} />
                )}

                <div className="flex min-w-0 flex-col gap-0.5">
                  <progress
                    aria-hidden
                    data-meilleur={String(meilleur)}
                    value={degats ?? 0}
                    max={maxDegats || 1}
                    className={cn(
                      dim.barre,
                      "w-full appearance-none overflow-hidden rounded-full bg-muted [&::-moz-progress-bar]:bg-emerald-700 [&::-webkit-progress-bar]:bg-muted [&::-webkit-progress-value]:bg-emerald-700",
                      meilleur &&
                        "shadow-[0_0_10px_rgb(187_247_208/0.6)] [&::-moz-progress-bar]:bg-emerald-200 [&::-webkit-progress-value]:bg-emerald-200",
                    )}
                  />
                  <span className={cn("truncate font-mono tabular-nums text-muted-foreground", dim.degats)}>
                    {millierLabel(degats)} ({parMinuteLabel(d?.degatsParMinute ?? null)})
                  </span>
                </div>

                <div className="flex justify-center">
                  {p.performance ? (
                    <AnneauDeNote note={p.performance.note} className={dim.note} />
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

function Mesure({ valeur, legende, dim }: { valeur: string; legende: string; dim: Densite }) {
  return (
    <div className="flex flex-col items-center leading-tight">
      <span className={cn("font-mono font-semibold tabular-nums", dim.mesure)}>{valeur}</span>
      <span className={cn("text-muted-foreground", dim.legende)}>{legende}</span>
    </div>
  );
}

/** Six objets, la babiole ronde, puis l'objet de rôle sans texte : doré terminé, grisé en cours, vide sans quête. */
function Objets({
  items,
  roleItem,
  compact,
}: {
  items: Array<number | null> | null;
  roleItem: Schemas["ObjetDeRole"] | null;
  compact: boolean;
}) {
  const taille = compact ? "size-5" : "size-7";
  const tailleRole = compact ? "size-5" : "size-8";
  const emplacements = Array.from({ length: 7 }, (_, i) => items?.[i] ?? null);
  const fini = roleItem?.etat === "Terminee";
  return (
    <div className={cn("flex items-center", compact ? "gap-1" : "gap-1.5")}>
      <ul
        aria-label="Objets"
        className={cn(
          "grid items-center gap-0.5",
          compact ? "grid-cols-[repeat(3,1.25rem)_1.25rem]" : "grid-cols-[repeat(3,1.75rem)_1.75rem]",
        )}
      >
        {emplacements.slice(0, 6).map((id, i) => (
          <li key={i}>
            <IconeObjet objetId={id} className={taille} />
          </li>
        ))}
        <li className="col-start-4 row-span-2 row-start-1 self-center">
          <IconeObjet objetId={emplacements[6]} className={cn(taille, "rounded-full")} />
        </li>
      </ul>
      {roleItem ? (
        <div
          role="group"
          aria-label={fini ? "Objet de rôle terminé" : "Objet de rôle en cours"}
          className={cn("rounded-lg", fini ? "ring-2 ring-amber-400" : "opacity-50 grayscale")}
        >
          <IconeObjet objetId={roleItem.id} className={tailleRole} />
        </div>
      ) : (
        <span aria-hidden className={cn("shrink-0 rounded-lg border border-dashed border-border", tailleRole)} />
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
