import { Link } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

/**
 * Bloc de game cliquable : un seul lien vers la page de la game, avec le bouton « Voir la game → »
 * dans l'en-tête et la bande « Scoreboard, stats… » en bas, visibles au repos.
 */
export function GameLinkCard({
  gameId,
  label,
  header,
  badge,
  children,
  className,
}: {
  gameId: number;
  label: string;
  header: ReactNode;
  badge: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <Link
      to="/games/$gameId"
      params={{ gameId: String(gameId) }}
      aria-label={label}
      className={cn(
        "group block cursor-pointer overflow-hidden rounded-lg border text-sm transition-all",
        "hover:border-white/70 hover:bg-white/5 hover:shadow-[0_0_18px_rgba(255,255,255,0.18)]",
        "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring",
        className,
      )}
    >
      <div className="flex items-center justify-between gap-2 border-b bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
        <span>{header}</span>
        <span className="flex items-center gap-2">
          {badge}
          <span className="rounded-md border px-2 py-0.5 font-medium text-foreground group-hover:border-white/70">
            Voir la game →
          </span>
        </span>
      </div>
      {children}
      <div className="border-t bg-muted/30 px-3 py-1.5 text-center text-xs text-muted-foreground group-hover:text-foreground">
        Scoreboard, stats, votes, pronostics et clips ›
      </div>
    </Link>
  );
}
