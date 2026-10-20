import { TriangleAlertIcon } from "lucide-react";
import { useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { equipeLabel, serieApres, vainqueurDeLaSerie } from "@/lib/effet-serie";
import { Pied, type Retour } from "./pied";
import { useRelire } from "./use-relire";

type Recap = Schemas["GameRecapResponse"];

/** Supprimer la game : ce qui sera recalculé, puis une case à cocher avant le bouton. */
export function OngletSupprimer({ game, onFermer, onSupprimee }: { game: Recap; onFermer: () => void; onSupprimee: () => void }) {
  const relire = useRelire();
  const [compris, setCompris] = useState(false);
  const [retour, setRetour] = useState<Retour>(null);
  const supprimer = $api.useMutation("delete", "/games/{id}", {
    onMutate: () => setRetour(null),
    onError: (erreur) => setRetour({ type: "erreur", erreur }),
    onSuccess: async () => {
      onSupprimee();
      await relire();
    },
  });

  const apres = serieApres(game, game.winner, null);
  const serieVidee = game.seriesId != null && game.teamAWins + game.teamBWins <= (game.winner ? 1 : 0);
  const gagnantAvant = vainqueurDeLaSerie(game);
  const gagnantApres = vainqueurDeLaSerie(apres);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-3">
        <TriangleAlertIcon className="size-5 text-destructive" aria-hidden />
        <h3 className="font-semibold">Supprimer la game #{game.id} ?</h3>
      </div>
      <p className="text-sm">La game disparaît de l'historique, des stats et des votes. Cette action ne peut pas être annulée.</p>
      <div className="flex flex-col gap-1.5 rounded-lg border bg-muted/40 p-3 text-sm">
        <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Ce qui sera recalculé</p>
        {game.seriesId != null &&
          (serieVidee ? (
            <p>La Série n'a plus de game : elle est effacée.</p>
          ) : (
            <p className="flex justify-between gap-2">
              <span>Série BO{game.bestOf}</span>{" "}
              <span>
                {game.teamAWins} – {game.teamBWins} → <strong>{apres.teamAWins} – {apres.teamBWins}</strong>
                {gagnantAvant !== gagnantApres && ` (vainqueur : ${equipeLabel(gagnantAvant)} → ${equipeLabel(gagnantApres)})`}
              </span>
            </p>
          ))}
        <p className="flex justify-between gap-2">
          <span>Costs du mois</span>{" "}
          <span className="text-muted-foreground">10 joueurs</span>
        </p>
      </div>
      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" checked={compris} onChange={(e) => setCompris(e.target.checked)} />
        Je comprends que c'est définitif
      </label>
      <Pied retour={retour}>
        <Button variant="outline" onClick={onFermer}>
          Annuler
        </Button>
        <Button variant="destructive" disabled={!compris || supprimer.isPending} onClick={() => supprimer.mutate({ params: { path: { id: game.id } } })}>
          Supprimer la game
        </Button>
      </Pied>
    </div>
  );
}
