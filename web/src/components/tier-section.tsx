import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

/** Un groupe de Joueurs d'un même tier (cost ou SoloQ). */
export function TierSection({
  label,
  title,
  count,
  className,
  children,
}: {
  label: string;
  title: ReactNode;
  count: number;
  className: string;
  children: ReactNode;
}) {
  return (
    <section aria-label={label} className={cn("overflow-hidden rounded-xl border", className)}>
      <header className="flex items-center justify-between border-b border-inherit px-4 py-3">
        <div className="flex items-center gap-2 font-bold">{title}</div>
        <span className="text-xs text-muted-foreground">
          {count} joueur{count > 1 ? "s" : ""}
        </span>
      </header>
      <div className="flex flex-wrap gap-3 bg-background/60 p-4">{children}</div>
    </section>
  );
}
