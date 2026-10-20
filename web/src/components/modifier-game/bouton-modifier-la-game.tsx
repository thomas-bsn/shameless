import { useQuery } from "@tanstack/react-query";
import { PencilIcon } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { sessionQuery } from "@/lib/session";
import { ModifierGameDialog } from "./modifier-game-dialog";

/** Le bouton de la page d'une game : pour les Administrateurs seulement. */
export function BoutonModifierLaGame({ gameId, onSupprimee }: { gameId: number; onSupprimee: () => void }) {
  const session = useQuery(sessionQuery);
  const [ouvert, setOuvert] = useState(false);
  if (session.data?.isAdmin !== true) return null;
  return (
    <>
      <Button variant="outline" size="sm" onClick={() => setOuvert(true)}>
        <PencilIcon />
        Modifier la game
      </Button>
      {ouvert && <ModifierGameDialog gameId={gameId} onFermer={() => setOuvert(false)} onSupprimee={onSupprimee} />}
    </>
  );
}
