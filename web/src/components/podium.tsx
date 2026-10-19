import { Link } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { Card } from "@/components/ui/card";
import { cn } from "@/lib/utils";

/** Le prénom d'un Joueur, lien vers sa fiche. */
export function PlayerLink({ playerId, prenom, className }: { playerId: number; prenom: string; className?: string }) {
  return (
    <Link to="/joueurs/$playerId" params={{ playerId: String(playerId) }} className={cn("font-semibold hover:underline", className)}>
      {prenom}
    </Link>
  );
}

/** Une marche du podium ; la première est plus haute et bordée de la couleur du classement. */
export function Marche({
  label,
  premier,
  bordure,
  children,
}: {
  /** Le nom accessible de la marche, par exemple « 1er : Sam ». */
  label: string;
  premier: boolean;
  /** Les classes de bordure de la première marche. */
  bordure: string;
  children: ReactNode;
}) {
  return (
    <li aria-label={label} className={cn("flex flex-1 flex-col", premier && "sm:-mt-4")}>
      <Card className={cn("items-center gap-2 px-3 py-5 text-center", premier && cn("py-7 ring-1", bordure))}>{children}</Card>
    </li>
  );
}
