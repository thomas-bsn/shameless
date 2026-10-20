import { useId, useState } from "react";
import type { Schemas } from "@/api/client";
import type { Participant, Side } from "@/components/game-equipe-table";
import { sidesOf } from "@/components/game-scoreboard";
import { IconeChampion } from "@/components/icones-de-jeu/icones-catalogue";
import { IconeObjectif, type Objectif } from "@/components/icones-de-jeu/icone-objectif";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Card, CardContent } from "@/components/ui/card";
import {
  type Chronologie,
  courbeDeGroupe,
  ecart,
  horloge,
  type Metrique,
  METRIQUES,
  metrique,
  type Nature,
  NATURE_LABEL,
  natureDe,
  type PointDeCourbe,
  signeLabel,
  traceLisse,
} from "@/lib/courbes";
import { nombreLabel } from "@/lib/game";
import { sideBorderClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";

type Recap = Schemas["GameRecapResponse"];

const TRAIT: Record<Side, string> = { blue: "stroke-sky-400", red: "stroke-red-400" };
const AIRE: Record<Side, string> = { blue: "fill-sky-400/15", red: "fill-red-400/15" };
const POINT: Record<Side, string> = { blue: "fill-sky-400", red: "fill-red-400" };
const TEXTE: Record<Side, string> = { blue: "text-sky-400", red: "text-red-400" };

const FOND_NATURE: Record<Nature, string> = {
  reelle: "bg-emerald-300",
  estimee: "bg-amber-400",
  indisponible: "bg-red-400",
};

/** La nature d'une série : réel, estimé ou indisponible. */
function PastilleDeNature({ nature }: { nature: Nature }) {
  return (
    <span className={cn("rounded-full px-1.5 py-px text-[10px] leading-4 font-bold text-zinc-950", FOND_NATURE[nature])}>
      {NATURE_LABEL[nature]}
    </span>
  );
}

interface Trace {
  cote: Side;
  points: PointDeCourbe[];
}

interface Cadre {
  largeur: number;
  hauteur: number;
  gauche: number;
  droite: number;
  haut: number;
  bas: number;
}

const GRAND: Cadre = { largeur: 800, hauteur: 300, gauche: 52, droite: 12, haut: 40, bas: 30 };
const PETIT: Cadre = { largeur: 220, hauteur: 84, gauche: 2, droite: 8, haut: 10, bas: 16 };

/** Le repère d'un graphe : du temps (en secondes) vers x, de la valeur vers y. */
function repere(cadre: Cadre, tMax: number, bas: number, haut: number) {
  const x = (t: number) =>
    tMax <= 0 ? cadre.gauche : cadre.gauche + (t / tMax) * (cadre.largeur - cadre.gauche - cadre.droite);
  const etendue = haut - bas || 1;
  const y = (v: number) =>
    cadre.hauteur - cadre.bas - ((v - bas) / etendue) * (cadre.hauteur - cadre.haut - cadre.bas);
  return { x, y };
}

/** Les traits discrets d'un graphe : l'axe du bas, une ligne intermédiaire et, si on les demande, leurs valeurs. */
function Grille({
  cadre,
  y,
  valeurs,
  etiquettes,
  zero,
}: {
  cadre: Cadre;
  y: (v: number) => number;
  valeurs: number[];
  etiquettes?: (v: number) => string;
  zero?: number;
}) {
  return (
    <g>
      {valeurs.map((v) => (
        <g key={v}>
          <line
            x1={cadre.gauche}
            x2={cadre.largeur - cadre.droite}
            y1={y(v)}
            y2={y(v)}
            className={v === zero ? "stroke-muted-foreground/50" : "stroke-foreground/10"}
            strokeDasharray={v === zero ? "4 4" : undefined}
          />
          {etiquettes && (
            <text x={cadre.gauche - 6} y={y(v) + 4} textAnchor="end" className="fill-muted-foreground text-[11px]">
              {etiquettes(v)}
            </text>
          )}
        </g>
      ))}
    </g>
  );
}

/** Une courbe lissée, son aire jusqu'à la ligne de base, et un point sur la dernière valeur. */
function Courbe({
  trace,
  x,
  y,
  base,
  aire,
  epaisseur,
  rayon,
}: {
  trace: Trace;
  x: (t: number) => number;
  y: (v: number) => number;
  base: number;
  aire: boolean;
  epaisseur: number;
  rayon: number;
}) {
  const xy = trace.points.map((p): [number, number] => [x(p.t), y(p.v)]);
  const dernier = xy[xy.length - 1];
  if (!dernier) return null;
  const premier = xy[0];
  const ligne = traceLisse(xy);
  return (
    <g>
      {aire && xy.length > 1 && (
        <path d={`${ligne} L${dernier[0]} ${base} L${premier[0]} ${base} Z`} className={cn("stroke-none", AIRE[trace.cote])} />
      )}
      {xy.length > 1 && (
        <path
          d={ligne}
          fill="none"
          strokeWidth={epaisseur}
          strokeLinecap="round"
          strokeLinejoin="round"
          className={TRAIT[trace.cote]}
        />
      )}
      <circle cx={dernier[0]} cy={dernier[1]} r={rayon} className={cn("stroke-none", POINT[trace.cote])} />
    </g>
  );
}

const OBJECTIFS: Record<string, { nom: string; objectif: Objectif }> = {
  dragon: { nom: "Dragon", objectif: "dragon" },
  herald: { nom: "Héraut", objectif: "heraut" },
  baron: { nom: "Baron", objectif: "baron" },
  turret: { nom: "Tourelle", objectif: "tourelle" },
  inhibitor: { nom: "Inhibiteur", objectif: "inhibiteur" },
};

const COTE_LABEL: Record<Side, string> = { blue: "bleu", red: "rouge" };
const COTE_EQUIPE: Record<Side, string> = { blue: "bleue", red: "rouge" };

/** L'infobulle d'un marqueur, dessinée au-dessus du reste du graphe et gardée dans ses bords. */
function Infobulle({ id, texte, cx, cy, largeurGraphe }: { id: string; texte: string; cx: number; cy: number; largeurGraphe: number }) {
  const largeur = texte.length * 6.2 + 16;
  const gauche = Math.min(Math.max(cx - largeur / 2, 2), largeurGraphe - largeur - 2);
  return (
    <g id={id} role="tooltip" className="pointer-events-none">
      <rect x={gauche} y={cy + 14} width={largeur} height={22} rx={4} className="fill-popover stroke-border" />
      <text x={gauche + largeur / 2} y={cy + 29} textAnchor="middle" className="fill-popover-foreground text-[11px]">
        {texte}
      </text>
    </g>
  );
}

/** Les marqueurs d'objectifs : un trait vertical à l'heure exacte et une pastille portant le logo, avec une infobulle au survol et au focus. */
function Marqueurs({
  evenements,
  tMax,
  cadre,
  x,
}: {
  evenements: Chronologie["evenements"];
  tMax: number;
  cadre: Cadre;
  x: (t: number) => number;
}) {
  const idInfobulle = useId();
  const [actif, activer] = useState<string | null>(null);
  const marqueurs = evenements
    .filter((e) => e.t <= tMax && OBJECTIFS[e.type] && (e.side === "blue" || e.side === "red"))
    .map((e) => {
      const cote = e.side as Side;
      const { nom, objectif } = OBJECTIFS[e.type];
      return { cle: `${e.type}-${e.t}-${e.side}`, t: e.t, cote, nom, objectif };
    });
  const survole = marqueurs.find((m) => m.cle === actif);
  return (
    <g>
      {marqueurs.map(({ cle, t, cote, nom, objectif }) => (
        <g
          key={cle}
          role="img"
          tabIndex={0}
          aria-label={`${nom} ${COTE_LABEL[cote]} à ${horloge(t)}`}
          aria-describedby={cle === actif ? idInfobulle : undefined}
          onMouseEnter={() => activer(cle)}
          onMouseLeave={() => activer((a) => (a === cle ? null : a))}
          onFocus={() => activer(cle)}
          onBlur={() => activer((a) => (a === cle ? null : a))}
          className="outline-none [&:focus-visible>circle]:stroke-[3]"
        >
          <line x1={x(t)} x2={x(t)} y1={cadre.haut - 12} y2={cadre.hauteur - cadre.bas} className="stroke-foreground/25" />
          <circle cx={x(t)} cy={cadre.haut - 20} r={10} className={cn("fill-muted", TRAIT[cote])} />
          <g aria-hidden transform={`translate(${x(t) - 7} ${cadre.haut - 27})`}>
            <IconeObjectif objectif={objectif} className="size-3.5 text-foreground" />
          </g>
        </g>
      ))}
      {survole && (
        <Infobulle
          id={idInfobulle}
          texte={`${survole.nom} · équipe ${COTE_EQUIPE[survole.cote]} · ${horloge(survole.t)}`}
          cx={x(survole.t)}
          cy={cadre.haut - 20}
          largeurGraphe={cadre.largeur}
        />
      )}
    </g>
  );
}

/** Les repères de temps du bas d'un graphe : le début, le milieu et la fin, en minutes. */
function RepresDeTemps({
  tMax,
  cadre,
  x,
  taille,
}: {
  tMax: number;
  cadre: Cadre;
  x: (t: number) => number;
  taille: string;
}) {
  const minutes = (t: number) => String(Math.round(t / 60));
  return (
    <g className={cn("fill-muted-foreground", taille)}>
      <text x={x(0)} y={cadre.hauteur - 4}>
        0
      </text>
      <text x={x(tMax / 2)} y={cadre.hauteur - 4} textAnchor="middle">
        {minutes(tMax / 2)}
      </text>
      <text x={x(tMax)} y={cadre.hauteur - 4} textAnchor="end">
        {minutes(tMax)} min
      </text>
    </g>
  );
}

function SansDonnees() {
  return (
    <p className="rounded-md border border-dashed py-10 text-center text-sm text-muted-foreground">
      Pas assez de données pour tracer cette courbe.
    </p>
  );
}

/** Les joueurs d'une équipe de la game. */
const idsDe = (players: Participant[]) => players.map((p) => p.playerId);

const SERIES_PETITES: Metrique["id"][] = ["kills", "cs", "niveau"];
const TITRES_PETITES: Record<string, string> = { kills: "Kills", cs: "CS", niveau: "Niveau moyen" };

/** Un petit graphe minimaliste : une courbe par équipe, la valeur finale de chacune en grand. */
function PetitGraphe({ game, chronologie, id, tMax }: { game: Recap; chronologie: Chronologie; id: Metrique["id"]; tMax: number }) {
  const m = metrique(id);
  const titre = TITRES_PETITES[id];
  const nature = natureDe(chronologie, m.serie);
  const traces: Trace[] = sidesOf(game).map((c) => ({
    cote: c.side,
    points: courbeDeGroupe(chronologie, idsDe(c.players), m),
  }));
  const max = Math.max(1, ...traces.flatMap((t) => t.points.map((p) => p.v)));
  const { x, y } = repere(PETIT, tMax, 0, max);
  const indisponible = nature === "indisponible";
  return (
    <Card role="group" aria-label={titre} className={cn("gap-2 py-4", indisponible && "opacity-50")}>
      <CardContent className="flex flex-col gap-2 px-4">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold">{titre}</h3>
          <PastilleDeNature nature={nature} />
        </div>
        <div className="flex gap-4 font-mono text-xl leading-tight font-bold tabular-nums">
          {traces.map((t) => (
            <span key={t.cote} className={TEXTE[t.cote]}>
              {t.points.length > 0 ? nombreLabel(t.points[t.points.length - 1].v, m.moyenne ? 1 : 0) : "–"}
            </span>
          ))}
        </div>
        {indisponible || traces.every((t) => t.points.length === 0) ? (
          <p className="py-4 text-xs text-muted-foreground">Pas de courbe</p>
        ) : (
          <svg viewBox={`0 0 ${PETIT.largeur} ${PETIT.hauteur}`} className="block w-full" role="img" aria-label={`${titre} au fil de la game`}>
            <Grille cadre={PETIT} y={y} valeurs={[0, max / 2]} />
            {traces.map((t, i) => (
              <Courbe key={t.cote} trace={t} x={x} y={y} base={y(0)} aire={i === 0} epaisseur={2} rayon={3.5} />
            ))}
            <RepresDeTemps tMax={tMax} cadre={PETIT} x={x} taille="text-[9px]" />
          </svg>
        )}
      </CardContent>
    </Card>
  );
}

/** La métrique affichée au départ : la première dont la série n'est pas indisponible. */
const metriqueParDefaut = (chronologie: Chronologie): Metrique =>
  METRIQUES.find((m) => natureDe(chronologie, m.serie) !== "indisponible") ?? METRIQUES[0];

/** Le grand graphe : l'avantage de l'équipe bleue sur la rouge, ou la courbe d'un joueur. */
function GrandGraphe({ game, chronologie, tMax }: { game: Recap; chronologie: Chronologie; tMax: number }) {
  const [choix, choisir] = useState<Metrique["id"] | null>(null);
  const [joueurId, choisirJoueur] = useState<number | null>(null);
  const m = choix ? metrique(choix) : metriqueParDefaut(chronologie);
  const cotes = sidesOf(game);
  const joueurs = cotes.flatMap((c) => c.players.map((p) => ({ p, side: c.side })));
  const joueur = joueurs.find((j) => j.p.playerId === joueurId);

  const [bleu, rouge] = cotes.map((c) => courbeDeGroupe(chronologie, idsDe(c.players), m));
  const points = joueur ? courbeDeGroupe(chronologie, [joueur.p.playerId], m) : ecart(bleu, rouge);
  const cote: Side = joueur?.side ?? "blue";
  const titre = joueur ? m.deJoueur(joueur.p.prenom) : m.avantage;

  const finale = points[points.length - 1];
  const decimales = m.moyenne ? 1 : 0;
  const finaleLabel = finale ? (joueur ? nombreLabel(finale.v, decimales) : signeLabel(finale.v, decimales)) : null;
  const nom = joueur
    ? `${titre} : ${finaleLabel} à ${horloge(finale?.t ?? 0)}`
    : `${titre} de l'équipe bleue sur la rouge : ${finaleLabel} à ${horloge(finale?.t ?? 0)}`;

  const extremum = Math.max(1, ...points.map((p) => Math.abs(p.v)));
  const bas = joueur ? 0 : -extremum;
  const { x, y } = repere(GRAND, tMax, bas, extremum);
  const ligneZero = joueur ? undefined : 0;

  return (
    <Card className="gap-3 py-5">
      <CardContent className="flex flex-col gap-4 px-5">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="mr-2 text-sm font-semibold tracking-wide text-muted-foreground uppercase">{titre}</h2>
          {METRIQUES.map((mt) => {
            const nature = natureDe(chronologie, mt.serie);
            const indisponible = nature === "indisponible";
            return (
              <button
                key={mt.id}
                type="button"
                disabled={indisponible}
                aria-pressed={mt.id === m.id}
                aria-label={`${mt.nom}, ${NATURE_LABEL[nature]}`}
                onClick={() => choisir(mt.id)}
                className={cn(
                  "flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-sm transition-colors outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50",
                  mt.id === m.id ? "bg-foreground/10 text-foreground" : "text-muted-foreground hover:text-foreground",
                  indisponible && "cursor-not-allowed opacity-40 hover:text-muted-foreground",
                )}
              >
                {mt.nom}
                <PastilleDeNature nature={nature} />
              </button>
            );
          })}
        </div>

        {points.length === 0 ? (
          <SansDonnees />
        ) : (
          <svg viewBox={`0 0 ${GRAND.largeur} ${GRAND.hauteur}`} className="block w-full">
            <Grille
              cadre={GRAND}
              y={y}
              valeurs={joueur ? [0, extremum / 2, extremum] : [-extremum, 0, extremum]}
              etiquettes={(v) => (joueur ? nombreLabel(v, decimales) : signeLabel(v, decimales))}
              zero={ligneZero}
            />
            <g role="img" aria-label={nom}>
              <Courbe trace={{ cote, points }} x={x} y={y} base={y(0)} aire epaisseur={2.5} rayon={4} />
            </g>
            <Marqueurs evenements={chronologie.evenements} tMax={tMax} cadre={GRAND} x={x} />
            <RepresDeTemps tMax={tMax} cadre={GRAND} x={x} taille="text-[11px]" />
          </svg>
        )}

        <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
          <span>Joueurs :</span>
          {joueurs.map(({ p, side }) => (
            <button
              key={p.playerId}
              type="button"
              aria-pressed={p.playerId === joueurId}
              onClick={() => choisirJoueur(p.playerId === joueurId ? null : p.playerId)}
              className={cn(
                "flex items-center gap-1.5 rounded-full border py-0.5 pr-2.5 pl-0.5 transition-colors outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50",
                sideBorderClass(side),
                p.playerId === joueurId ? "bg-foreground/10 text-foreground" : "opacity-70 hover:opacity-100",
              )}
            >
              <span aria-hidden className="flex">
                <IconeChampion championId={p.championId} className="size-5 rounded-full" />
              </span>
              {p.prenom}
            </button>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

/** L'onglet Courbes : l'avantage entre équipes au fil de la game, puis trois petits graphes (kills, CS, niveau). */
export function OngletCourbes({ game }: { game: Recap }) {
  const chronologie = game.chronologie;
  if (!chronologie || chronologie.points.length === 0) {
    return (
      <Alert>
        <AlertDescription>
          <strong>Cette game n'a pas de chronologie.</strong> Les courbes viennent d'un Rapport envoyé par un Companion à
          jour pendant la partie ; les games enregistrées sans, ou saisies à la main, n'en ont pas.
        </AlertDescription>
      </Alert>
    );
  }
  const tMax = Math.max(...chronologie.points.map((p) => p.t));
  return (
    <div className="flex flex-col gap-4">
      <GrandGraphe game={game} chronologie={chronologie} tMax={tMax} />
      <div className="grid gap-4 sm:grid-cols-3">
        {SERIES_PETITES.map((id) => (
          <PetitGraphe key={id} game={game} chronologie={chronologie} id={id} tMax={tMax} />
        ))}
      </div>
    </div>
  );
}
