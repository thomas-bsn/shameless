import { useEffect, useState } from "react";
import type { Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";

type Picker = Schemas["PickerResponse"];

const PAS_MS = 450;

/** Vrai quand le système demande de réduire les animations : le tirage s'affiche alors d'un coup. */
export function reduireLesAnimations() {
  return typeof window.matchMedia === "function" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

/** Les joueurs dans l'ordre où le tirage les place : un dans chaque Équipe à tour de rôle. */
function ordreDuTirage(picker: Picker) {
  const ordre: { prenom: string; equipe: "A" | "B" }[] = [];
  for (let i = 0; i < Math.max(picker.teamA.length, picker.teamB.length); i++) {
    const a = picker.teamA[i];
    const b = picker.teamB[i];
    if (a) ordre.push({ prenom: a.prenom, equipe: "A" });
    if (b) ordre.push({ prenom: b.prenom, equipe: "B" });
  }
  return ordre;
}

/**
 * Le tirage de la Roulette dans une fenêtre : les joueurs sont placés un à un puis la fenêtre se
 * referme sur le résultat. « Passer l'animation » la ferme tout de suite.
 */
export function TirageDialog({ picker, onClose }: { picker: Picker; onClose: () => void }) {
  const ordre = ordreDuTirage(picker);
  const [places, setPlaces] = useState(0);

  useEffect(() => {
    if (places >= ordre.length) {
      const fin = setTimeout(onClose, PAS_MS);
      return () => clearTimeout(fin);
    }
    const pas = setTimeout(() => setPlaces((n) => n + 1), PAS_MS);
    return () => clearTimeout(pas);
  }, [places, ordre.length, onClose]);

  const courant = ordre[Math.min(places, ordre.length - 1)];
  const dans = (equipe: "A" | "B") => ordre.slice(0, places).filter((p) => p.equipe === equipe);

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent aria-label="Tirage en cours" aria-labelledby={undefined}>
        <DialogHeader>
          <DialogTitle>Tirage en cours</DialogTitle>
          <DialogDescription>Roulette {picker.variante === "Aleatoire" ? "aléatoire" : "équilibrée"}</DialogDescription>
        </DialogHeader>
        <p aria-hidden className="py-4 text-center text-2xl font-semibold motion-safe:animate-pulse">
          {courant?.prenom}
        </p>
        <div className="grid grid-cols-2 gap-4">
          {(["A", "B"] as const).map((equipe) => (
            <div key={equipe} className="flex flex-col gap-1">
              <p className="text-sm font-medium">Équipe {equipe}</p>
              <ul className="flex flex-col gap-1">
                {Array.from({ length: 5 }, (_, i) => (
                  <li key={i} className="rounded-md border border-dashed px-2 py-1 text-sm text-muted-foreground">
                    {dans(equipe)[i]?.prenom ?? "…"}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
        <div className="flex items-center justify-between gap-3">
          <div className="flex flex-1 flex-col gap-1">
            <progress aria-label="Progression du tirage" className="h-2 w-full" value={places} max={ordre.length} />
            <span className="text-xs text-muted-foreground">
              {places} joueur{places > 1 ? "s" : ""} placé{places > 1 ? "s" : ""} sur {ordre.length}
            </span>
          </div>
          <Button variant="ghost" onClick={onClose}>
            Passer l'animation
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
