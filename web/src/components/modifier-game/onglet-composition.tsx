import { useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import { posteLabel } from "@/lib/game";
import { Pied, type Retour } from "./pied";
import { useRelire } from "./use-relire";

type Recap = Schemas["GameRecapResponse"];
type Equipes = { A: number[]; B: number[] };

const CHAMP_NATIF = "h-9 w-full min-w-0 rounded-md border border-input bg-background px-2 text-sm shadow-xs";
const COULEURS = { A: "bg-blue-400", B: "bg-red-400" } as const;

const composition = (game: Recap): Equipes => ({ A: game.teamA.map((p) => p.playerId), B: game.teamB.map((p) => p.playerId) });

/** La composition des deux équipes : un joueur déjà placé est grisé dans toutes les listes. */
export function OngletComposition({ game, onFermer }: { game: Recap; onFermer: () => void }) {
  const actuelle = composition(game);
  return <Formulaire key={JSON.stringify(actuelle)} game={game} actuelle={actuelle} onFermer={onFermer} />;
}

function Formulaire({ game, actuelle, onFermer }: { game: Recap; actuelle: Equipes; onFermer: () => void }) {
  const relire = useRelire();
  const joueurs = $api.useQuery("get", "/players");
  const [equipes, setEquipes] = useState<Equipes>(actuelle);
  const [retour, setRetour] = useState<Retour>(null);
  const corriger = $api.useMutation("put", "/games/{id}/correction", {
    onMutate: () => setRetour(null),
    onError: (erreur) => setRetour({ type: "erreur", erreur }),
    onSuccess: async () => {
      setRetour({ type: "ok", texte: "Enregistré." });
      await relire();
    },
  });

  const noms = new Map<number, string>([...game.teamA, ...game.teamB].map((p) => [p.playerId, p.prenom]));
  for (const j of joueurs.data ?? []) noms.set(j.id, j.prenom);
  const options = [...noms].sort((a, b) => a[1].localeCompare(b[1], "fr"));

  const choisir = (equipe: "A" | "B", index: number, id: number) => {
    setRetour(null);
    setEquipes((e) => ({ ...e, [equipe]: e[equipe].map((v, i) => (i === index ? id : v)) }));
  };
  const placeDans = (id: number, equipe: "A" | "B", index: number) => {
    for (const e of ["A", "B"] as const) {
      const i = equipes[e].indexOf(id);
      if (i >= 0 && !(e === equipe && i === index)) return e;
    }
    return null;
  };
  const modifiees = (["A", "B"] as const).reduce((n, e) => n + equipes[e].filter((id, i) => id !== actuelle[e][i]).length, 0);

  return (
    <div className="flex flex-col gap-4">
      {joueurs.isError && <ErrorAlert error={joueurs.error} />}
      <div className="grid gap-5 sm:grid-cols-2">
        {(["A", "B"] as const).map((equipe) => (
          <div key={equipe} className="flex flex-col gap-2">
            <span className="flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
              <span className={`size-2 rounded-full ${COULEURS[equipe]}`} aria-hidden />
              Équipe {equipe} · {equipe === "A" ? game.teamASide : game.teamBSide}
            </span>
            {equipes[equipe].map((id, i) => {
              const poste = posteLabel((equipe === "A" ? game.teamA : game.teamB)[i]?.stats?.position);
              return (
                <div key={i} className="flex items-center gap-2">
                  <span className="w-16 shrink-0 text-xs text-muted-foreground">{poste ?? `Joueur ${i + 1}`}</span>
                  <select
                    aria-label={`Équipe ${equipe}, joueur ${i + 1}`}
                    className={CHAMP_NATIF}
                    value={id}
                    onChange={(e) => choisir(equipe, i, Number(e.target.value))}
                  >
                    {options.map(([joueurId, prenom]) => {
                      const ailleurs = placeDans(joueurId, equipe, i);
                      return (
                        <option key={joueurId} value={joueurId} disabled={ailleurs !== null}>
                          {ailleurs ? `${prenom} (déjà en équipe ${ailleurs})` : prenom}
                        </option>
                      );
                    })}
                  </select>
                </div>
              );
            })}
          </div>
        ))}
      </div>
      <Pied info={modifiees > 0 ? `${modifiees} changement${modifiees > 1 ? "s" : ""} non enregistré${modifiees > 1 ? "s" : ""}` : undefined} retour={retour}>
        <Button variant="outline" onClick={onFermer}>
          Fermer
        </Button>
        <Button
          disabled={modifiees === 0 || corriger.isPending}
          onClick={() => corriger.mutate({ params: { path: { id: game.id } }, body: { winner: null, teamA: equipes.A, teamB: equipes.B } })}
        >
          Enregistrer la composition
        </Button>
      </Pied>
    </div>
  );
}
