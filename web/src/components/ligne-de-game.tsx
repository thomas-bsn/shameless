import { Link } from "@tanstack/react-router";
import { useState } from "react";
import type { Schemas } from "@/api/client";
import { AnneauDeNote } from "@/components/icones-de-jeu/anneau-de-note";
import { BadgeDeTitre } from "@/components/icones-de-jeu/badge-de-titre";
import { IconeRole } from "@/components/icones-de-jeu/icone-role";
import { IconeChampion, IconeObjet, IconeRune, IconeSort } from "@/components/icones-de-jeu/icones-catalogue";
import { dateHeureLabel } from "@/lib/dates";
import { decimalLabel, kdaDetailLabel } from "@/lib/fiche";
import { csParMinute, dureeLabel, ratioKda } from "@/lib/game";
import { cn } from "@/lib/utils";

type Game = Schemas["GameDeLaFiche"];

const placeLabel = (place: number) => (place === 1 ? "1er" : `${place}e`);

/** Résultat, heure, série et durée : ce que la ligne dit de la game avant son champion. */
function Contexte({ game: g }: { game: Game }) {
  return (
    <div className="flex w-24 shrink-0 flex-col leading-tight">
      <span className="text-xs text-muted-foreground">{dateHeureLabel(g.playedAt)}</span>
      <span className={cn("text-[15px] font-bold", g.victoire ? "text-emerald-400" : "text-red-400")}>
        {g.victoire ? "Victoire" : "Défaite"}
      </span>
      <span className="text-xs text-muted-foreground">
        {[g.seriesId != null ? `Série ${g.seriesId}` : null, g.dureeSecondes != null ? dureeLabel(g.dureeSecondes) : null]
          .filter(Boolean)
          .join(" · ")}
      </span>
    </div>
  );
}

/** Le portrait du champion, ses sorts et ses runes (masqués sans donnée) et l'icône de son rôle. */
function Portrait({ game: g }: { game: Game }) {
  const s = g.stats;
  const sorts = s != null && (s.spell1Id != null || s.spell2Id != null);
  const runes = s != null && (s.perks?.[0] != null || s.perkSubStyle != null);
  return (
    <div className="relative flex shrink-0 items-center gap-1">
      <IconeChampion
        championId={g.championId}
        className={cn("size-13 rounded-[10px] border-2", g.victoire ? "border-emerald-400" : "border-red-400")}
      />
      {sorts && (
        <div className="flex flex-col gap-0.5">
          <IconeSort sortId={s.spell1Id} className="size-6" />
          <IconeSort sortId={s.spell2Id} className="size-6" />
        </div>
      )}
      {runes && (
        <div className="flex flex-col gap-0.5">
          <IconeRune runeId={s.perks?.[0]} className="size-6 rounded-full" />
          <IconeRune runeId={s.perkSubStyle} className="size-6 rounded-full" />
        </div>
      )}
      {g.role && (
        <IconeRole role={g.role} className="absolute -bottom-1 -left-1 size-5 rounded bg-background p-0.5 text-sky-400" />
      )}
    </div>
  );
}

function Kda({ game: g }: { game: Game }) {
  return (
    <div className="flex w-24 shrink-0 flex-col items-center gap-1 leading-tight">
      <span className="font-mono text-xl font-bold tabular-nums">{kdaDetailLabel(g.stats)}</span>
      <span className="flex gap-1">
        {g.mvp && <BadgeDeTitre titre="MVP" />}
        {g.badge === "Ace" && <BadgeDeTitre titre="ACE" />}
        {g.dofd && <BadgeDeTitre titre="DOFD" />}
        {g.badge === "MeilleurePerf" && <span className="text-[10px] font-semibold text-sky-300">Meilleure perf</span>}
      </span>
    </div>
  );
}

function Ratios({ game: g }: { game: Game }) {
  const kda = g.stats ? ratioKda(g.stats) : null;
  const cs = g.stats ? csParMinute(g.stats, g.dureeSecondes) : null;
  return (
    <div className="flex w-20 shrink-0 flex-col text-xs leading-relaxed text-foreground/80">
      {kda != null && <span className="font-semibold text-sky-300">{decimalLabel(kda)} KDA</span>}
      {cs != null && <span>{decimalLabel(cs)} CS/m</span>}
      {g.participationAuxKills != null && <span>{decimalLabel(g.participationAuxKills, 0)} % KP</span>}
    </div>
  );
}

/** Six objets puis la babiole. */
function Objets({ items }: { items: Array<number | null> }) {
  return (
    <div className="flex items-center gap-1.5 rounded-[10px] border bg-background/70 p-1.5">
      <ul aria-label="Objets" className="grid grid-cols-3 gap-0.5">
        {Array.from({ length: 6 }, (_, i) => (
          <li key={i}>
            <IconeObjet objetId={items[i] ?? null} className="size-6" />
          </li>
        ))}
      </ul>
      <span aria-hidden className="h-8 w-px bg-border" />
      <IconeObjet objetId={items[6] ?? null} className="size-6 rounded-full" />
    </div>
  );
}

function Adversaire({ championId }: { championId: string }) {
  return (
    <div className="relative shrink-0">
      <IconeChampion championId={championId} className="size-11 rounded-lg opacity-90" />
      <span className="absolute -bottom-2 left-1/2 -translate-x-1/2 rounded border bg-background px-1.5 text-[10px] font-bold">
        VS
      </span>
    </div>
  );
}

function Note({ game: g }: { game: Game }) {
  if (!g.performance) return <span className="ml-auto text-muted-foreground">–</span>;
  return (
    <div className="ml-auto flex shrink-0 items-center gap-2 rounded-full border bg-background/70 py-1 pr-1.5 pl-3">
      <span className="text-lg font-bold tabular-nums">{g.performance.note}</span>
      {g.place != null && <span className="text-xs text-muted-foreground">{placeLabel(g.place)}</span>}
      <AnneauDeNote note={g.performance.note} className="size-8" />
    </div>
  );
}

const FOND = {
  victoire: "border-emerald-500/35 bg-gradient-to-r from-emerald-500/15 to-card to-50%",
  defaite: "border-red-500/35 bg-gradient-to-r from-red-500/15 to-card to-50%",
} as const;

/**
 * Une game du profil public, en ligne dense : un clic ouvre la game, la flèche déplie l'accès à ses onglets.
 * Seul ce que l'API fournit s'affiche (ni LP ni or).
 */
export function LigneDeGame({ game: g }: { game: Game }) {
  const [depliee, setDepliee] = useState(false);
  return (
    <div
      className={cn(
        "relative rounded-xl border transition-shadow hover:shadow-[0_8px_24px_rgba(0,0,0,0.5)] hover:outline hover:outline-white/30",
        g.victoire ? FOND.victoire : FOND.defaite,
      )}
    >
      <Link
        to="/games/$gameId"
        params={{ gameId: String(g.gameId) }}
        aria-label={`Voir la game ${g.gameId}`}
        className="flex min-h-21 flex-wrap items-center gap-x-3 gap-y-2 rounded-xl py-2.5 pr-12 pl-4 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        <Contexte game={g} />
        {g.stats == null ? (
          <span className="flex flex-1 flex-col text-sm leading-tight">
            <span className="font-medium">Stats non saisies</span>
            <span className="text-xs text-muted-foreground">Une fois le Rapport importé, la ligne se remplit.</span>
          </span>
        ) : (
          <>
            <Portrait game={g} />
            <Kda game={g} />
            <Ratios game={g} />
            {g.stats.items && <Objets items={g.stats.items} />}
            {g.adversaireChampionId && <Adversaire championId={g.adversaireChampionId} />}
            <Note game={g} />
          </>
        )}
      </Link>
      <button
        type="button"
        aria-label={`${depliee ? "Replier" : "Déplier"} la game ${g.gameId}`}
        aria-expanded={depliee}
        onClick={() => setDepliee((d) => !d)}
        className="absolute top-3 right-3 flex size-8 items-center justify-center rounded-md text-muted-foreground hover:bg-white/10 hover:text-foreground"
      >
        <svg
          viewBox="0 0 14 14"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.6"
          strokeLinecap="round"
          aria-hidden
          className={cn("size-4 transition-transform", depliee && "rotate-180")}
        >
          <path d="M3 5l4 4 4-4" />
        </svg>
      </button>
      {depliee && (
        <Link
          to="/games/$gameId"
          params={{ gameId: String(g.gameId) }}
          className="block rounded-b-xl border-t bg-card px-4 py-2.5 text-center text-sm text-muted-foreground hover:text-foreground"
        >
          Scoreboard, stats, votes, pronostics et clips ›
        </Link>
      )}
    </div>
  );
}
