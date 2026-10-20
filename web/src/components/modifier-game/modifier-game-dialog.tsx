import { useState } from "react";
import { $api } from "@/api/client";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { dateHeureLabel, dateLabel } from "@/lib/dates";
import { OngletComposition } from "./onglet-composition";
import { OngletRapport } from "./onglet-rapport";
import { OngletStats } from "./onglet-stats";
import { OngletResultat } from "./onglet-resultat";
import { OngletSupprimer } from "./onglet-supprimer";

/**
 * La fenêtre « Modifier la game » (maquette A) : Résultat, Composition, Stats, Rapport et Supprimer.
 * Une correction se pose par-dessus les données du Companion et s'annule ; seule la suppression est définitive.
 */
export function ModifierGameDialog({ gameId, onFermer, onSupprimee }: { gameId: number; onFermer: () => void; onSupprimee?: () => void }) {
  const game = $api.useQuery("get", "/games/{id}", { params: { path: { id: gameId } } });
  const [onglet, setOnglet] = useState("resultat");

  return (
    <Dialog open onOpenChange={(ouvert) => !ouvert && onFermer()}>
      <DialogContent className="max-h-[90vh] gap-4 overflow-y-auto sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>Modifier la game #{gameId}</DialogTitle>
          <DialogDescription>
            {game.data
              ? [
                  game.data.winner ? `Équipe ${game.data.winner} gagne` : "Sans vainqueur",
                  `jouée le ${dateHeureLabel(game.data.playedAt)}`,
                  game.data.modifieeParAdminLe ? `modifiée par un admin le ${dateLabel(game.data.modifieeParAdminLe)}` : null,
                ]
                  .filter(Boolean)
                  .join(" · ")
              : "Corriger les données de la game."}
          </DialogDescription>
        </DialogHeader>
        {game.isPending ? (
          <LoadingRows />
        ) : game.isError ? (
          <ErrorAlert error={game.error} />
        ) : (
          <Tabs value={onglet} onValueChange={setOnglet}>
            <TabsList variant="line" className="w-full justify-start border-b">
              <TabsTrigger value="resultat" className="flex-none">
                Résultat
              </TabsTrigger>
              <TabsTrigger value="composition" className="flex-none">
                Composition
              </TabsTrigger>
              <TabsTrigger value="stats" className="flex-none">
                Stats
              </TabsTrigger>
              <TabsTrigger value="rapport" className="flex-none">
                Rapport
              </TabsTrigger>
              <TabsTrigger value="supprimer" className="ml-auto flex-none text-destructive">
                Supprimer
              </TabsTrigger>
            </TabsList>
            <TabsContent value="resultat" className="pt-2">
              <OngletResultat game={game.data} onFermer={onFermer} />
            </TabsContent>
            <TabsContent value="composition" className="pt-2">
              <OngletComposition game={game.data} onFermer={onFermer} />
            </TabsContent>
            <TabsContent value="stats" className="pt-2">
              <OngletStats game={game.data} onFermer={onFermer} />
            </TabsContent>
            <TabsContent value="rapport" className="pt-2">
              <OngletRapport gameId={gameId} onFermer={onFermer} />
            </TabsContent>
            <TabsContent value="supprimer" className="pt-2">
              <OngletSupprimer
                game={game.data}
                onFermer={onFermer}
                onSupprimee={() => {
                  onFermer();
                  onSupprimee?.();
                }}
              />
            </TabsContent>
          </Tabs>
        )}
      </DialogContent>
    </Dialog>
  );
}
