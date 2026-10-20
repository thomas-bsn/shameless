import { Link } from "@tanstack/react-router";
import { useState } from "react";
import type { Schemas } from "@/api/client";
import { AnneauDeNote } from "@/components/icones-de-jeu/anneau-de-note";
import { BadgeDeTitre } from "@/components/icones-de-jeu/badge-de-titre";
import { IconeRole } from "@/components/icones-de-jeu/icone-role";
import { IconeChampion, IconeObjet, IconeRune, IconeSort } from "@/components/icones-de-jeu/icones-catalogue";
import { dateHeureLabel, ilYALabel } from "@/lib/dates";
import { decimalLabel } from "@/lib/fiche";
import { csParMinute, dureeLabel, ratioKda, titreDeBadge } from "@/lib/game";
import { PanneauDeGame } from "@/components/panneau-de-game";
import { cn } from "@/lib/utils";

type Game = Schemas["GameDeLaFiche"];

const placeLabel = (place: number) => (place === 1 ? "1er" : `${place}e`);

/** Durée et ancienneté, le type de partie, puis le résultat : ce que la ligne dit de la game avant son champion. */
function Contexte({ game: g }: { game: Game }) {
  return (
    <div className="flex w-28 shrink-0 flex-col gap-0.5 leading-tight whitespace-nowrap">
      <span className="text-xs text-muted-foreground" title={dateHeureLabel(g.playedAt)}>
        {g.dureeSecondes != null && <>{dureeLabel(g.dureeSecondes)} </>}
        {ilYALabel(g.playedAt)}
      </span>
      <span className="text-base font-bold text-foreground">Custom</span>
      <span className={cn("text-xs font-semibold", g.victoire ? "text-emerald-400" : "text-red-400")}>
        {g.victoire ? "Victoire" : "Défaite"}
      </span>
    </div>
  );
}

/** Le portrait du champion et l'icône de son rôle. */
function Portrait({ game: g }: { game: Game }) {
  return (
    <div className="relative shrink-0">
      <IconeChampion
        championId={g.championId}
        className={cn("size-16 rounded-xl border-2", g.victoire ? "border-emerald-400" : "border-red-400")}
      />
      {g.role && (
        <IconeRole role={g.role} className="absolute -bottom-1 -left-1 size-5 rounded bg-background p-0.5 text-sky-400" />
      )}
    </div>
  );
}

function Kda({ game: g }: { game: Game }) {
  const titre = titreDeBadge(g.badge);
  const v = (n: number | null | undefined) => (n == null ? "–" : String(n));
  return (
    <div className="flex min-w-28 shrink-0 flex-col items-center gap-1 leading-tight whitespace-nowrap">
      <span className="font-mono text-[28px] leading-none font-bold whitespace-nowrap tabular-nums">
        {v(g.stats?.kills)}
        <span className="font-light text-muted-foreground"> / </span>
        <span className="text-red-300">{v(g.stats?.deaths)}</span>
        <span className="font-light text-muted-foreground"> / </span>
        {v(g.stats?.assists)}
      </span>
      <span className="flex flex-nowrap gap-1 whitespace-nowrap">
        {titre && <BadgeDeTitre titre={titre} />}
        {g.dofd && <BadgeDeTitre titre="DOFD" />}
      </span>
    </div>
  );
}

function Ratios({ game: g }: { game: Game }) {
  const kda = g.stats ? ratioKda(g.stats) : null;
  const cs = g.stats ? csParMinute(g.stats, g.dureeSecondes) : null;
  return (
    <div className="hidden w-20 shrink-0 flex-col text-xs leading-relaxed whitespace-nowrap text-foreground/80 @min-[36rem]:flex">
      {kda != null && <span className="font-semibold text-sky-300">{decimalLabel(kda)} KDA</span>}
      {cs != null && <span>{decimalLabel(cs)} CS/m</span>}
      {g.participationAuxKills != null && <span>{decimalLabel(g.participationAuxKills, 0)} % KP</span>}
    </div>
  );
}

/** Une boîte sombre : sorts et runes en 2×2, un séparateur, puis six objets (4 + 2) et la babiole ronde. */
function Equipement({ game: g }: { game: Game }) {
  const s = g.stats;
  const items = s?.items ?? [];
  return (
    <div className="hidden shrink-0 items-center gap-2 rounded-xl border bg-background/70 p-1.5 @min-[52rem]:flex">
      <div className="grid grid-cols-2 gap-0.5">
        <IconeSort sortId={s?.spell1Id} className="size-8" />
        <IconeRune runeId={s?.perks?.[0]} className="size-8 rounded-full" />
        <IconeSort sortId={s?.spell2Id} className="size-8" />
        <IconeRune runeId={s?.perkSubStyle} className="size-8 rounded-full" />
      </div>
      {s?.items && (
        <>
          <span aria-hidden className="h-14 w-px bg-border" />
          <div className="grid grid-cols-4 gap-0.5">
            <ul aria-label="Objets" className="contents">
              {Array.from({ length: 6 }, (_, i) => (
                <li key={i}>
                  <IconeObjet objetId={items[i] ?? null} className="size-7" />
                </li>
              ))}
            </ul>
            <IconeObjet objetId={items[6] ?? null} className="size-7 rounded-full" />
          </div>
        </>
      )}
    </div>
  );
}

function Adversaire({ championId }: { championId: string }) {
  return (
    <div className="hidden shrink-0 items-center gap-2 @min-[60rem]:flex">
      <span aria-hidden className="h-px w-3 bg-border" />
      <div className="relative">
        <IconeChampion championId={championId} className="size-14 rounded-lg opacity-90" />
        <span className="absolute -bottom-2 left-1/2 -translate-x-1/2 rounded border bg-background px-1.5 text-[10px] font-bold">
          VS
        </span>
      </div>
    </div>
  );
}

function Note({ game: g }: { game: Game }) {
  if (!g.performance) return <span className="ml-auto shrink-0 text-muted-foreground">–</span>;
  return (
    <div className="ml-auto flex shrink-0 items-center gap-2 whitespace-nowrap">
      <div className="flex flex-col items-center rounded-xl border bg-background/70 px-3 py-1 leading-tight">
        <span className="text-xl font-bold tabular-nums">{g.performance.note}</span>
        {g.place != null && <span className="text-[11px] text-muted-foreground">{placeLabel(g.place)}</span>}
      </div>
      <AnneauDeNote note={g.performance.note} className="size-9" sansValeur />
    </div>
  );
}

const FOND = {
  victoire: "border-emerald-500/35 bg-gradient-to-r from-emerald-500/15 to-card to-50%",
  defaite: "border-red-500/35 bg-gradient-to-r from-red-500/15 to-card to-50%",
} as const;

/**
 * Une game du profil public, en ligne dense : un clic ouvre la game, le chevron déplie son panneau (le récapitulatif des deux équipes).
 * Seul ce que l'API fournit s'affiche (ni LP ni or).
 */
export function LigneDeGame({ game: g }: { game: Game }) {
  const [depliee, setDepliee] = useState(false);
  return (
    <div
      className={cn(
        "@container relative rounded-xl border transition-shadow hover:shadow-[0_8px_24px_rgba(0,0,0,0.5)] hover:outline hover:outline-white/30",
        g.victoire ? FOND.victoire : FOND.defaite,
      )}
    >
      <Link
        to="/games/$gameId"
        params={{ gameId: String(g.gameId) }}
        aria-label={`Voir la game ${g.gameId}`}
        className="flex min-h-24 flex-nowrap items-center gap-x-3 rounded-xl py-2 pr-14 pl-4 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
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
            <Equipement game={g} />
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
        className={cn(
          "absolute inset-y-0 right-0 flex w-11 items-center justify-center rounded-r-xl border-l-2 text-muted-foreground hover:bg-white/10 hover:text-foreground",
          g.victoire ? "border-emerald-500/60" : "border-red-500/60",
        )}
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
      {depliee && <PanneauDeGame gameId={g.gameId} />}
    </div>
  );
}
