import type { ElementDuCatalogue } from "@/lib/catalogue";
import { cn } from "@/lib/utils";

/**
 * L'icône d'un objet, d'un sort ou d'une rune. Sans id, un emplacement vide ; avec un id que le catalogue
 * ne connaît pas (ou indisponible), l'id lui-même : jamais d'erreur.
 */
export function IconeDeJeu({
  id,
  element,
  className,
}: {
  id: number | null | undefined;
  element: ElementDuCatalogue | undefined;
  className?: string;
}) {
  const base = cn("flex size-7 shrink-0 items-center justify-center overflow-hidden rounded-md", className);
  if (id == null) return <span aria-hidden className={cn(base, "border border-dashed border-border bg-muted/20")} />;
  if (!element)
    return (
      <span title={`Id ${id}`} className={cn(base, "bg-muted font-mono text-[9px] tabular-nums text-muted-foreground")}>
        {id}
      </span>
    );
  return (
    <span className={base}>
      <img src={element.iconUrl} alt={element.name} title={element.name} className="size-full object-cover" />
    </span>
  );
}
