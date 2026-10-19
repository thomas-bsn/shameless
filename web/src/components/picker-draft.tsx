import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { points } from "@/lib/cost";
import { joueursLibres } from "@/lib/picker";
import { cn } from "@/lib/utils";
import { AvatarJoueur } from "@/components/avatar-joueur";

type Picker = Schemas["PickerResponse"];
type Equipe = "A" | "B";

const capitaineDe = (picker: Picker, equipe: Equipe) =>
  picker.players.find((p) => p.playerId === (equipe === "A" ? picker.capitaineA : picker.capitaineB));

/**
 * Le pile ou face dans une fenêtre : il désigne le premier à choisir. L'API a déjà tiré au sort ;
 * « Lancer la pièce » révèle le résultat.
 */
export function PileOuFaceDialog({ picker, onClose }: { picker: Picker; onClose: () => void }) {
  const [revele, setRevele] = useState(false);
  const premier = picker.premierPick === "B" ? "B" : "A";
  const capitaine = capitaineDe(picker, premier);

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent aria-label="Pile ou face" aria-labelledby={undefined}>
        <DialogHeader>
          <DialogTitle>Qui choisit en premier ?</DialogTitle>
          <DialogDescription>Pile ou face entre les deux capitaines.</DialogDescription>
        </DialogHeader>
        <ul className="flex flex-wrap gap-2 text-sm">
          {(["A", "B"] as const).map((equipe) => (
            <li key={equipe} className="rounded-md border px-3 py-1">
              {capitaineDe(picker, equipe)?.prenom}, Équipe {equipe} : <strong>{equipe === "A" ? "Pile" : "Face"}</strong>
            </li>
          ))}
        </ul>
        <p aria-live="polite" className="min-h-8 text-center text-xl font-semibold">
          {revele && `${capitaine?.prenom} choisit en premier`}
        </p>
        <DialogFooter>
          {revele ? (
            <Button onClick={onClose}>Commencer la Draft</Button>
          ) : (
            <Button onClick={() => setRevele(true)}>Lancer la pièce</Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Le capitaine choisit un joueur ; la fenêtre montre le cost de son équipe avant et après. */
export function ChoixCapitaineDialog({ picker, onClose }: { picker: Picker; onClose: () => void }) {
  const queryClient = useQueryClient();
  const [choisi, setChoisi] = useState<number | null>(null);
  const equipe = picker.equipeAuTrait as Equipe;
  const capitaine = capitaineDe(picker, equipe);
  const pick = $api.useMutation("post", "/soirees/open/picker/draft/picks", {
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["get", "/soirees/open"] }),
  });
  const libres = joueursLibres(picker);
  const joueur = libres.find((p) => p.playerId === choisi);
  const costEquipe = equipe === "A" ? picker.teamACost : picker.teamBCost;
  const costAdverse = equipe === "A" ? picker.teamBCost : picker.teamACost;
  const numero = picker.teamA.length + picker.teamB.length + 1;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent aria-label="Choix du capitaine" aria-labelledby={undefined}>
        <DialogHeader>
          <DialogDescription>
            Équipe {equipe} · choix {numero} sur {picker.players.length}
          </DialogDescription>
          <DialogTitle>{capitaine?.prenom}, à toi de choisir</DialogTitle>
        </DialogHeader>
        <div role="radiogroup" aria-label="Joueurs libres" className="grid grid-cols-2 gap-2">
          {libres.map((p) => (
            <button
              key={p.playerId}
              type="button"
              role="radio"
              aria-checked={p.playerId === choisi}
              onClick={() => setChoisi(p.playerId)}
              className={cn(
                "flex items-center justify-between rounded-md border px-3 py-2 text-sm hover:bg-accent",
                p.playerId === choisi && "border-primary bg-accent",
              )}
            >
              <span className="flex min-w-0 items-center gap-2">
                <AvatarJoueur prenom={p.prenom} avatarUrl={p.avatarUrl} className="size-6 text-[10px]" />
                <span className="truncate">{p.prenom}</span>
              </span>
              <span className="font-mono text-xs text-muted-foreground">{points(p.cost ?? 0)}</span>
            </button>
          ))}
        </div>
        <div className="rounded-md border p-3 text-sm">
          <p>
            {joueur
              ? `Équipe ${equipe} : ${points(costEquipe)} → ${points(costEquipe + (joueur.cost ?? 0))} avec ${joueur.prenom}`
              : `Équipe ${equipe} : ${points(costEquipe)}`}
          </p>
          <p className="text-muted-foreground">{`Équipe ${equipe === "A" ? "B" : "A"} : ${points(costAdverse)}`}</p>
        </div>
        {pick.isError && <ErrorAlert error={pick.error} />}
        <DialogFooter>
          <Button variant="ghost" onClick={onClose}>
            Annuler
          </Button>
          <Button
            disabled={!joueur || pick.isPending}
            onClick={() => {
              if (joueur) pick.mutate({ body: { equipe, playerId: joueur.playerId } }, { onSuccess: () => setChoisi(null) });
            }}
          >
            {joueur ? `Choisir ${joueur.prenom}` : "Choisir"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
