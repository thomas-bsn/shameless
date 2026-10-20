import { ChevronLeft, ChevronRight } from "lucide-react";
import { useState } from "react";
import { $api } from "@/api/client";
import { ModifierGameDialog } from "@/components/modifier-game/modifier-game-dialog";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { dateLabel } from "@/lib/dates";
import { type Month, monthLabel, shiftMonth } from "@/lib/history";
import { cn } from "@/lib/utils";

const currentMonth = (): Month => {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
};

function ModifieeParAdmin({ le, className }: { le: string | null | undefined; className?: string }) {
  if (!le) return null;
  return <span className={cn("text-xs text-muted-foreground", className)}>modifiée par un admin le {dateLabel(le)}</span>;
}

/** L'onglet Games : les games terminées d'un mois, à corriger ou supprimer (ADR 0009). */
export function GamesAdmin() {
  const [mois, setMois] = useState<Month>(currentMonth);
  const [ouverte, setOuverte] = useState<number | null>(null);
  const maintenant = currentMonth();
  const suivant = shiftMonth(mois, 1);
  const aSuivant = suivant.year * 12 + suivant.month <= maintenant.year * 12 + maintenant.month;
  const historique = $api.useQuery("get", "/games/history/{year}/{month}", {
    params: { path: { year: mois.year, month: mois.month } },
  });

  const games = (historique.data?.games ?? []).flatMap((s) => s.games).filter((g) => g.winner === "A" || g.winner === "B");

  return (
    <Card>
      <CardHeader>
        <CardTitle>Games</CardTitle>
        <CardDescription>
          Corrigez le vainqueur, la composition ou les stats d'une game terminée, ou supprimez-la. Les données du Companion ne sont jamais
          modifiées : une correction se pose par-dessus et s'annule.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <nav aria-label="Mois" className="flex items-center gap-2">
          <Button variant="outline" size="icon" aria-label="Mois précédent" onClick={() => setMois(shiftMonth(mois, -1))}>
            <ChevronLeft />
          </Button>
          <span className="min-w-36 text-center font-medium">{monthLabel(mois)}</span>
          <Button variant="outline" size="icon" aria-label="Mois suivant" disabled={!aSuivant} onClick={() => setMois(suivant)}>
            <ChevronRight />
          </Button>
        </nav>
        {historique.isPending ? (
          <LoadingRows />
        ) : historique.isError ? (
          <ErrorAlert error={historique.error} />
        ) : games.length === 0 ? (
          <p className="text-sm text-muted-foreground">Aucune game terminée ce mois-ci.</p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Game</TableHead>
                <TableHead>Jouée le</TableHead>
                <TableHead>Vainqueur</TableHead>
                <TableHead>Action</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {games.map((g) => (
                <TableRow key={g.id}>
                  <TableCell className="font-medium">#{g.id}</TableCell>
                  <TableCell>{dateLabel(g.playedAt)}</TableCell>
                  <TableCell className="whitespace-normal">
                    <div className="flex flex-col items-start gap-1">
                      <span>Équipe {g.winner}</span>
                      <ModifieeParAdmin le={g.modifieeParAdminLe} />
                    </div>
                  </TableCell>
                  <TableCell>
                    <Button variant="outline" size="sm" aria-label={`Ouvrir la game #${g.id}`} onClick={() => setOuverte(g.id)}>
                      Corriger
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
      {ouverte !== null && <ModifierGameDialog gameId={ouverte} onFermer={() => setOuverte(null)} />}
    </Card>
  );
}
