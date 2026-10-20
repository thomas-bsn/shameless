import { useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { equipeLabel, serieApres, vainqueurDeLaSerie } from "@/lib/effet-serie";
import { Pied, type Retour } from "./pied";
import { useRelire } from "./use-relire";

type Recap = Schemas["GameRecapResponse"];

const EQUIPES = [
  { id: "A", couleur: "bg-blue-400" },
  { id: "B", couleur: "bg-red-400" },
] as const;

/** Qui a gagné : l'équipe gagnante, et ce que ce changement fait à la Série et aux costs. */
export function OngletResultat({ game, onFermer }: { game: Recap; onFermer: () => void }) {
  const relire = useRelire();
  const [choix, setChoix] = useState<string | null>(null);
  const [retour, setRetour] = useState<Retour>(null);
  const vers = choix ?? game.winner;
  const change = vers !== game.winner;
  const corriger = $api.useMutation("put", "/games/{id}/correction", {
    onMutate: () => setRetour(null),
    onError: (erreur) => setRetour({ type: "erreur", erreur }),
    onSuccess: async () => {
      setChoix(null);
      setRetour({ type: "ok", texte: "Enregistré." });
      await relire();
    },
  });

  const avantSerie = vainqueurDeLaSerie(game);
  const apres = serieApres(game, game.winner, vers);

  return (
    <div className="flex flex-col gap-4">
      <fieldset className="flex flex-col gap-2">
        <legend className="mb-2 text-sm font-medium">Équipe gagnante</legend>
        <div className="grid gap-3 sm:grid-cols-2">
          {EQUIPES.map(({ id, couleur }) => (
            <label
              key={id}
              className="flex h-14 cursor-pointer items-center gap-3 rounded-lg border px-4 has-[:checked]:border-foreground has-[:checked]:bg-muted"
            >
              <input type="radio" name="vainqueur" checked={vers === id} onChange={() => setChoix(id)} />
              <span className={`size-2 rounded-full ${couleur}`} aria-hidden />
              Équipe {id} · {id === "A" ? game.teamASide : game.teamBSide}
            </label>
          ))}
        </div>
      </fieldset>
      {change && (
        <div className="flex flex-col gap-1.5 rounded-lg border bg-muted/40 p-3 text-sm">
          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Effet de ce changement</p>
          {game.seriesId != null && (
            <>
              <p className="flex justify-between gap-2">
                <span>Série BO{game.bestOf}</span>{" "}
                <span>
                  {game.teamAWins} – {game.teamBWins} → <strong>{apres.teamAWins} – {apres.teamBWins}</strong>
                </span>
              </p>
              {avantSerie !== vainqueurDeLaSerie(apres) && (
                <p className="flex justify-between gap-2">
                  <span>Vainqueur de la Série</span>{" "}
                  <span>
                    {equipeLabel(avantSerie)} → <strong>{equipeLabel(vainqueurDeLaSerie(apres))}</strong>
                  </span>
                </p>
              )}
            </>
          )}
          <p className="flex justify-between gap-2">
            <span>Costs recalculés</span>{" "}
            <span className="text-muted-foreground">10 joueurs</span>
          </p>
        </div>
      )}
      <Pied info={change ? "1 changement non enregistré" : undefined} retour={retour}>
        <Button variant="outline" onClick={onFermer}>
          Fermer
        </Button>
        <Button
          disabled={!change || corriger.isPending}
          onClick={() => corriger.mutate({ params: { path: { id: game.id } }, body: { winner: vers, teamA: null, teamB: null } })}
        >
          Enregistrer
        </Button>
      </Pied>
    </div>
  );
}
