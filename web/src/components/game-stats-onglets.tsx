import type { ReactNode } from "react";
import type { Schemas } from "@/api/client";
import { type Participant, TableauEquipe } from "@/components/game-equipe-table";
import { sidesOf } from "@/components/game-scoreboard";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  millierLabel,
  nombreLabel,
  parMinute,
  parMinuteLabel,
  partLabel,
  sommeConnue,
  statLabel,
} from "@/lib/game";
import { sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";

type Recap = Schemas["GameRecapResponse"];
type Stats = Schemas["StatsDuParticipant"];

/** Un indicateur lu sur les stats d'un joueur ; absent (null) quand le Companion ne l'a pas envoyé. */
type Mesure = (s: Stats) => number | null | undefined;

interface Contexte {
  durationSeconds: number | null;
  /** Le total de l'équipe du joueur, pour la part de l'équipe. */
  totalEquipe: (mesure: Mesure) => number | null;
  /** Le plus grand total de la Game pour une mesure, pour proportionner les barres. */
  maxBarre: (mesure: Mesure) => number;
}

interface Colonne {
  titre: string;
  cell: (p: Participant, ctx: Contexte) => ReactNode;
}

interface Tuile {
  nom: string;
  mesure: Mesure;
  /** Si défini, la tuile montre la valeur par minute (avec ces décimales) plutôt que le total. */
  parMinute?: number;
  format?: (total: number | null) => string;
}

interface Onglet {
  tuiles: Tuile[];
  colonnes: Colonne[];
  /** Le résumé d'une équipe, à droite de son titre. */
  meta: (total: (m: Mesure) => number | null) => string;
}

const valeur = (p: Participant, mesure: Mesure): number | null => (p.stats ? (mesure(p.stats) ?? null) : null);

const dommages: Mesure = (s) => s.damageToChampions;
const subis: Mesure = (s) => s.damageTaken;
const or: Mesure = (s) => s.goldEarned;
const tours: Mesure = (s) => s.turretKills;
const dragons: Mesure = (s) => s.dragonKills;
const barons: Mesure = (s) => s.baronKills;
const objectifs: Mesure = (s) => s.damageToObjectives;
const vision: Mesure = (s) => s.visionScore;
const posees: Mesure = (s) => s.wardsPlaced;
const detruites: Mesure = (s) => s.wardsKilled;
const controle: Mesure = (s) => s.controlWardsBought;

const nombre =
  (mesure: Mesure): Colonne["cell"] =>
  (p) => <span className="font-mono tabular-nums">{statLabel(valeur(p, mesure))}</span>;

const enMilliers = (titre: string, mesure: Mesure, muted = false): Colonne => ({
  titre,
  cell: (p, ctx) => <BarreValeur value={valeur(p, mesure)} max={ctx.maxBarre(mesure)} muted={muted} />,
});

const parMinuteColonne = (mesure: Mesure, decimales: number): Colonne => ({
  titre: "Par minute",
  cell: (p, ctx) => (
    <span className="font-mono tabular-nums">
      {parMinuteLabel(parMinute(valeur(p, mesure), ctx.durationSeconds), decimales)}
    </span>
  ),
});

const total = (n: number | null) => nombreLabel(n);

const ONGLETS = {
  degats: {
    tuiles: [
      { nom: "Dégâts infligés", mesure: dommages, format: millierLabel },
      { nom: "Dégâts par minute", mesure: dommages, parMinute: 0 },
      { nom: "Or gagné", mesure: or, format: millierLabel },
    ],
    colonnes: [
      enMilliers("Dégâts infligés", dommages),
      parMinuteColonne(dommages, 0),
      {
        titre: "Part de l'équipe",
        cell: (p, ctx) => (
          <span className="font-mono tabular-nums">{partLabel(valeur(p, dommages), ctx.totalEquipe(dommages))}</span>
        ),
      },
      enMilliers("Dégâts subis", subis, true),
    ],
    meta: (t) => `${millierLabel(t(dommages))} infligés`,
  },
  objectifs: {
    tuiles: [
      { nom: "Tours", mesure: tours, format: total },
      { nom: "Dragons", mesure: dragons, format: total },
      { nom: "Barons", mesure: barons, format: total },
    ],
    colonnes: [
      { titre: "Tours", cell: nombre(tours) },
      { titre: "Dragons", cell: nombre(dragons) },
      { titre: "Barons", cell: nombre(barons) },
      enMilliers("Dégâts aux objectifs", objectifs),
    ],
    meta: (t) => `${statLabel(t(tours))} tours · ${statLabel(t(dragons))} dragons · ${statLabel(t(barons))} barons`,
  },
  vision: {
    tuiles: [
      { nom: "Score de vision", mesure: vision, format: total },
      { nom: "Balises posées", mesure: posees, format: total },
      { nom: "Balises détruites", mesure: detruites, format: total },
    ],
    colonnes: [
      {
        titre: "Score de vision",
        cell: (p, ctx) => <BarreValeur value={valeur(p, vision)} max={ctx.maxBarre(vision)} brut />,
      },
      parMinuteColonne(vision, 1),
      { titre: "Posées", cell: nombre(posees) },
      { titre: "Détruites", cell: nombre(detruites) },
      { titre: "Contrôle", cell: nombre(controle) },
    ],
    meta: (t) => `${statLabel(t(vision))} de score`,
  },
} satisfies Record<string, Onglet>;

export type NomOnglet = keyof typeof ONGLETS;

/** Une valeur et une barre proportionnelle au plus gros total de la Game ; pas de barre sans valeur. */
function BarreValeur({
  value,
  max,
  muted,
  brut,
}: {
  value: number | null;
  max: number;
  muted?: boolean;
  brut?: boolean;
}) {
  return (
    <div className="flex min-w-24 flex-col gap-1">
      <span className="font-mono tabular-nums">{brut ? statLabel(value) : millierLabel(value)}</span>
      {value != null && (
        <progress
          aria-hidden
          value={value}
          max={max}
          className={cn(
            "h-1 w-full appearance-none overflow-hidden rounded-full bg-muted [&::-moz-progress-bar]:bg-current [&::-webkit-progress-bar]:bg-muted [&::-webkit-progress-value]:bg-current",
            muted ? "text-muted-foreground" : "text-foreground",
          )}
        />
      )}
    </div>
  );
}

/** Un onglet de stats : trois tuiles de totaux par équipe (bleu puis rouge), puis un tableau par équipe. */
export function OngletStats({ game, onglet }: { game: Recap; onglet: NomOnglet }) {
  const { tuiles, colonnes, meta }: Onglet = ONGLETS[onglet];
  const cotes = sidesOf(game);
  const durationSeconds = game.match?.durationSeconds ?? null;
  const sommeDe = (players: Participant[], mesure: Mesure) => sommeConnue(players.map((p) => valeur(p, mesure)));
  const maxBarre = (mesure: Mesure) => Math.max(1, ...cotes.flatMap((c) => c.players.map((p) => valeur(p, mesure) ?? 0)));

  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-[repeat(auto-fit,minmax(150px,1fr))] gap-4">
        {tuiles.map((t) => (
          <Card key={t.nom} role="group" aria-label={t.nom} className="gap-2 py-4">
            <CardHeader className="px-4">
              <CardTitle className="text-sm font-medium text-muted-foreground">{t.nom}</CardTitle>
            </CardHeader>
            <CardContent className="flex items-baseline justify-between gap-2 px-4 font-mono text-lg font-semibold tabular-nums">
              {cotes.map((c) => {
                const somme = sommeDe(c.players, t.mesure);
                const texte =
                  t.parMinute != null
                    ? nombreLabel(parMinute(somme, durationSeconds), t.parMinute)
                    : (t.format ?? total)(somme);
                return (
                  <span key={c.side} className={sideTextClass(c.side)}>
                    {texte}
                  </span>
                );
              })}
            </CardContent>
          </Card>
        ))}
      </div>
      {cotes.map((c) => {
        const ctx: Contexte = {
          durationSeconds,
          totalEquipe: (m) => sommeDe(c.players, m),
          maxBarre,
        };
        return (
          <TableauEquipe
            key={c.side}
            team={c.team}
            side={c.side}
            players={c.players}
            resume={meta((m) => sommeDe(c.players, m))}
            colonnes={colonnes}
            ligne={(p) => ({ cellules: colonnes.map((col) => col.cell(p, ctx)) })}
          />
        );
      })}
    </div>
  );
}

/** L'onglet Replay : grisé, il dit pourquoi le replay 3D n'existe pas encore (Phase 3). */
export function OngletReplay() {
  return (
    <Card>
      <CardContent className="flex flex-col gap-3">
        <h2 className="text-lg font-semibold">Le replay 3D arrive bientôt</h2>
        <p className="text-sm text-muted-foreground">
          Il rejouera les déplacements des dix joueurs, les kills et les objectifs, avec une frise pour avancer dans la
          game.
        </p>
        <p className="text-sm text-muted-foreground">
          Il a besoin des positions des joueurs, que Shameless ne récupère pas encore. Elles viendront du Companion et
          du proxchat.
        </p>
      </CardContent>
    </Card>
  );
}
