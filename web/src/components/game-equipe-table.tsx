import type { ReactNode } from "react";
import type { Schemas } from "@/api/client";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useChampions } from "@/lib/draft-champions-queries";
import { posteLabel } from "@/lib/game";
import { sideBorderClass, sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";
import { AvatarJoueur } from "@/components/avatar-joueur";

export type Participant = Schemas["GameHistoryParticipantDto"];
export type Side = "blue" | "red";

export const sideName = (side: Side) => (side === "blue" ? "Blue" : "Red");

/** L'illustration du champion (image du jeu), sinon un carré avec son initiale ; le niveau en coin. */
export function ChampionCarre({
  championId,
  nom,
  image,
  niveau,
  side,
}: {
  championId: string | null;
  nom?: string;
  image?: string;
  niveau?: number | null;
  side: Side;
}) {
  return (
    <span className={cn("relative flex size-10 shrink-0 items-center justify-center rounded-md border-2 bg-muted text-sm font-semibold", sideBorderClass(side))}>
      {image ? <img src={image} alt="" className="size-full rounded-sm object-cover" /> : <span aria-hidden>{(nom ?? championId ?? "?").charAt(0)}</span>}
      {niveau != null && (
        <span
          data-slot="niveau"
          className="absolute -right-1 -bottom-1 rounded bg-background px-1 font-mono text-[10px] leading-4 tabular-nums"
        >
          {niveau}
        </span>
      )}
    </span>
  );
}

export interface ColonneDuTableau {
  titre: string;
  /** Les chiffres sont alignés à droite. */
  droite?: boolean;
}

export interface LigneDuTableau {
  /** Classes de la ligne (entourage du MVP ou du DOFD). */
  className?: string;
  /** Les pastilles à côté du prénom. */
  badges?: ReactNode;
  /** La cellule à gauche du joueur (son rôle), quand le tableau a une colonne « avant ». */
  avant?: ReactNode;
  /** Une cellule par colonne, dans l'ordre. */
  cellules: ReactNode[];
}

/**
 * Le tableau d'une équipe : la colonne Joueur (champion, prénom, poste) puis les colonnes de l'onglet.
 * <code>ligne</code> fournit pour chaque joueur ses cellules.
 */
export function TableauEquipe({
  team,
  side,
  resume,
  colonneAvant,
  colonnes,
  players,
  ligne,
}: {
  team: string;
  side: Side;
  /** Ce qui s'affiche à droite du titre de l'équipe : l'issue ou un résumé. */
  resume: ReactNode;
  /** Le titre d'une colonne placée à gauche du joueur ; absente, pas de colonne. */
  colonneAvant?: string;
  colonnes: ColonneDuTableau[];
  players: Participant[];
  ligne: (p: Participant) => LigneDuTableau;
}) {
  const champions = useChampions();
  const catalogue = champions.data ?? [];
  const label = `Équipe ${team} · ${sideName(side)} side`;

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between gap-2">
        <CardTitle className={cn("text-sm", sideTextClass(side))}>{label}</CardTitle>
        <span className="text-sm font-medium text-muted-foreground">{resume}</span>
      </CardHeader>
      <CardContent className="px-2 sm:px-6">
        <table aria-label={label} className="w-full border-separate border-spacing-y-1 text-xs sm:text-sm">
          <thead className="text-xs text-muted-foreground">
            <tr>
              {colonneAvant && (
                <th scope="col" className="px-1 text-left font-medium sm:px-2">
                  {colonneAvant}
                </th>
              )}
              <th scope="col" className="px-1 text-left font-medium sm:px-2">
                Joueur
              </th>
              {colonnes.map((c) => (
                <th key={c.titre} scope="col" className={cn("px-1 font-medium sm:px-2", c.droite ? "text-right" : "text-left")}>
                  {c.titre}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {players.map((p) => {
              const champion = catalogue.find((c) => c.id === p.championId);
              const { className, badges, avant, cellules } = ligne(p);
              return (
                <tr
                  key={p.playerId}
                  aria-label={p.prenom}
                  className={cn("[&>td]:bg-muted/30 [&>td:first-child]:rounded-l-lg [&>td:last-child]:rounded-r-lg", className)}
                >
                  {colonneAvant && <td className="px-1 font-medium sm:px-2">{avant}</td>}
                  <td className="px-1 py-2 sm:px-2">
                    <div className="flex items-center gap-2 sm:gap-3">
                      <ChampionCarre championId={p.championId} nom={champion?.name} image={champion?.imageUrl} niveau={p.stats?.champLevel} side={side} />
                      <div className="flex min-w-0 flex-col">
                        <span className="flex flex-wrap items-center gap-1 font-medium">
                          <AvatarJoueur prenom={p.prenom} avatarUrl={p.avatarUrl} className="size-5 text-[9px]" />
                          <span className="truncate">{p.prenom}</span>
                          {badges}
                        </span>
                        <span className="text-xs text-muted-foreground">
                          {[champion?.name ?? p.championId, posteLabel(p.stats?.position)].filter(Boolean).join(" · ") || "Champion inconnu"}
                        </span>
                      </div>
                    </div>
                  </td>
                  {cellules.map((cellule, i) => (
                    <td key={colonnes[i]!.titre} className={cn("px-1 sm:px-2", colonnes[i]!.droite && "text-right")}>
                      {cellule}
                    </td>
                  ))}
                </tr>
              );
            })}
          </tbody>
        </table>
      </CardContent>
    </Card>
  );
}
