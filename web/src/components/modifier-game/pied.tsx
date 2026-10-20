import type { ReactNode } from "react";
import { problemMessage } from "@/lib/problem";

/** Ce que l'Administrateur doit voir après un enregistrement : l'erreur ou la confirmation. */
export type Retour = { type: "erreur"; erreur: unknown } | { type: "ok"; texte: string } | null;

/**
 * Le pied d'un onglet : l'information, l'erreur et la confirmation s'affichent à côté des boutons,
 * là où l'œil est après le clic, et non en haut de la fenêtre.
 */
export function Pied({ info, retour, children }: { info?: string; retour?: Retour; children: ReactNode }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-4">
      <div className="flex min-w-0 flex-1 flex-col gap-0.5 text-sm">
        {info && <span className="text-muted-foreground">{info}</span>}
        {retour?.type === "erreur" && (
          <span role="alert" className="text-destructive">
            {problemMessage(retour.erreur)}
          </span>
        )}
        {retour?.type === "ok" && <span role="status">{retour.texte}</span>}
      </div>
      <div className="flex gap-2">{children}</div>
    </div>
  );
}
