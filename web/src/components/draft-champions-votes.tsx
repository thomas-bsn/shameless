import { useEffect, useRef, useState } from "react";
import type { Schemas } from "@/api/client";
import { ErrorAlert } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useMaintenant } from "@/components/draft-champions-view";

type Draft = Schemas["DraftChampionsResponse"];

export type GenreDeVote = "pause" | "reprise" | "reset";

/** Une demande en attente de la réponse de l'autre Capitaine : pause, reprise anticipée ou Reset. */
export type Vote = { genre: GenreDeVote; parEquipe: string; expireLe: string };

const TITRES: Record<GenreDeVote, string> = {
  pause: "Pause demandée",
  reprise: "Reprise demandée",
  reset: "Reset demandé",
};

function questionDe(vote: Vote): string {
  switch (vote.genre) {
    case "pause":
      return `L'Équipe ${vote.parEquipe} demande une pause de la draft (5 minutes au plus).`;
    case "reprise":
      return `L'Équipe ${vote.parEquipe} demande la reprise de la draft.`;
    case "reset":
      return `L'Équipe ${vote.parEquipe} demande à remettre la draft à zéro.`;
  }
}

/** La demande en attente de la draft (une seule à la fois côté API), ou null. */
export function voteEnCours(draft: Draft): Vote | null {
  if (draft.terminee) return null;
  if (draft.resetDemande)
    return { genre: "reset", parEquipe: draft.resetDemande.equipe, expireLe: draft.resetDemande.expireLe };
  if (draft.pauseDemandee)
    return { genre: "pause", parEquipe: draft.pauseDemandee.parEquipe, expireLe: draft.pauseDemandee.expireLe };
  if (draft.repriseDemandee)
    return { genre: "reprise", parEquipe: draft.repriseDemandee.parEquipe, expireLe: draft.repriseDemandee.expireLe };
  return null;
}

/** Durée d'affichage d'un avis passager (« Draft en pause » quand la pause démarre). */
const DUREE_AVIS_MS = 5000;

/**
 * Les votes de la draft : le Capitaine qui doit répondre les voit dans une fenêtre modale au centre de
 * l'écran (titre, qui demande, décompte, Accepter / Refuser) ; le demandeur et les spectateurs, dans un
 * avis centré qui ne bloque rien. Tout disparaît quand la demande est résolue ou expirée.
 */
export function VotesDeLaDraft({
  draft,
  equipe,
  enCours,
  erreur,
  onRepondre,
}: {
  draft: Draft;
  equipe: string | null;
  enCours: boolean;
  erreur: unknown;
  onRepondre: (genre: GenreDeVote, accepte: boolean) => void;
}) {
  const maintenant = useMaintenant(250);
  const vote = voteEnCours(draft);
  const secondes = vote ? Math.max(0, Math.ceil((Date.parse(vote.expireLe) - maintenant) / 1000)) : 0;
  const enAttente = vote !== null && secondes > 0;

  const avisPasse = useAvisDePause(draft, equipe === null, enAttente);

  if (enAttente && vote) {
    if (equipe !== null && equipe !== vote.parEquipe) {
      return (
        <Dialog open>
          <DialogContent
            className="sm:max-w-md [&>button:last-child]:hidden"
            onEscapeKeyDown={(e) => e.preventDefault()}
            onInteractOutside={(e) => e.preventDefault()}
          >
            <DialogHeader>
              <DialogTitle>{TITRES[vote.genre]}</DialogTitle>
              <DialogDescription>{questionDe(vote)}</DialogDescription>
            </DialogHeader>
            <p className="text-sm text-muted-foreground">
              Sans réponse dans{" "}
              <span aria-label="Temps pour répondre" className="font-semibold tabular-nums text-foreground">
                {secondes} s
              </span>
              , rien ne change.
            </p>
            {erreur != null && <ErrorAlert error={erreur} />}
            <DialogFooter>
              <Button variant="outline" disabled={enCours} onClick={() => onRepondre(vote.genre, false)}>
                Refuser
              </Button>
              <Button disabled={enCours} onClick={() => onRepondre(vote.genre, true)}>
                Accepter
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      );
    }
    const texte =
      equipe === vote.parEquipe ? "En attente de la réponse de l'autre Capitaine." : questionDe(vote);
    return (
      <Avis titre={TITRES[vote.genre]}>
        <span>{texte}</span>
        <span>
          Sans réponse dans{" "}
          <span aria-label="Temps pour répondre" className="font-semibold tabular-nums">
            {secondes} s
          </span>
          , rien ne change.
        </span>
      </Avis>
    );
  }

  return avisPasse ? <Avis titre={avisPasse} /> : null;
}

/** Un avis centré qui laisse passer les clics : on peut continuer à regarder la draft. */
function Avis({ titre, children }: { titre: string; children?: React.ReactNode }) {
  return (
    <div className="pointer-events-none fixed inset-x-0 top-1/3 z-40 flex justify-center px-4">
      <div
        role="status"
        data-testid="avis-de-vote"
        className="flex max-w-md flex-col gap-1 rounded-lg border bg-background p-5 text-center shadow-lg"
      >
        <p className="text-lg font-semibold">{titre}</p>
        {children && <div className="flex flex-col gap-1 text-sm text-muted-foreground">{children}</div>}
      </div>
    </div>
  );
}

/** Pour un spectateur : « Draft en pause » pendant quelques secondes quand une demande de pause aboutit. */
function useAvisDePause(draft: Draft, spectateur: boolean, enAttente: boolean): string | null {
  const [avis, setAvis] = useState<string | null>(null);
  const demandeVue = useRef(false);
  const enPause = draft.pause != null;

  useEffect(() => {
    if (enAttente) {
      demandeVue.current = draft.pauseDemandee != null;
      return;
    }
    if (spectateur && demandeVue.current && enPause) {
      demandeVue.current = false;
      setAvis("Draft en pause");
      const id = setTimeout(() => setAvis(null), DUREE_AVIS_MS);
      return () => clearTimeout(id);
    }
    demandeVue.current = false;
  }, [enAttente, enPause, spectateur, draft.pauseDemandee]);

  return avis;
}
