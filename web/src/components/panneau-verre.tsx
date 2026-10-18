import { Slot } from "radix-ui";
import type * as React from "react";
import { cn } from "@/lib/utils";

/** Les classes de la surface en verre dépoli, pour les éléments qui ne passent pas par le composant. */
export const classesPanneauVerre = "border border-verre-bord bg-verre-fond shadow-verre backdrop-blur-verre";

/**
 * La surface en verre dépoli du design « Verre nuit » : fond translucide, flou d'arrière-plan et bord fin,
 * posée par-dessus le Fond. `asChild` la pose sur l'élément de son choix (section, aside, article…).
 */
export function PanneauVerre({
  className,
  asChild = false,
  ...props
}: React.ComponentProps<"div"> & { asChild?: boolean }) {
  const Comp = asChild ? Slot.Root : "div";
  return <Comp className={cn(classesPanneauVerre, className)} {...props} />;
}
