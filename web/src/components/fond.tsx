import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { $api } from "@/api/client";
import { clipVideoUrl } from "@/components/clip-video";
import { sessionQuery } from "@/lib/session";

/** Écran étroit : sous le point de rupture `md` de Tailwind (la sidebar devient un tiroir). */
const ECRAN_ETROIT = "(max-width: 767px)";
const MOINS_D_ANIMATIONS = "(prefers-reduced-motion: reduce)";

function useMediaQuery(query: string): boolean {
  const subscribe = (onChange: () => void) => {
    const media = window.matchMedia?.(query);
    media?.addEventListener("change", onChange);
    return () => media?.removeEventListener("change", onChange);
  };
  return useSyncExternalStore(subscribe, () => window.matchMedia?.(query).matches ?? false, () => false);
}

/**
 * Le Fond : derrière les pages à session, une vidéo muette qui enchaîne des Clips de l'Instance, floutée
 * sous un voile sombre dans les deux thèmes. Un décor fixe prend le relais sans session, sans Clip, sur écran étroit, avec la
 * réduction des animations, ou quand aucun clip ne se lit. Un seul clip est chargé à la fois.
 */
export function Fond() {
  const session = useQuery(sessionQuery);
  const etroit = useMediaQuery(ECRAN_ETROIT);
  const mouvementReduit = useMediaQuery(MOINS_D_ANIMATIONS);
  const actif = !!session.data && !etroit && !mouvementReduit;

  const clips = $api.useQuery("get", "/clips/fond", {}, { enabled: actif, staleTime: Infinity, refetchOnWindowFocus: false });
  const [illisibles, setIllisibles] = useState<number[]>([]);
  const [rang, setRang] = useState(0);
  const video = useRef<HTMLVideoElement>(null);

  const lisibles = (clips.data ?? []).filter((clip) => !illisibles.includes(clip.id));
  const courant = actif && lisibles.length > 0 ? lisibles[rang % lisibles.length] : undefined;

  // La lecture s'arrête quand la page est cachée, et ne reprend que si le Fond est actif.
  useEffect(() => {
    if (!actif) return;
    const surVisibilite = () => {
      if (document.hidden) video.current?.pause?.();
      else void video.current?.play?.()?.catch(() => {});
    };
    document.addEventListener("visibilitychange", surVisibilite);
    return () => document.removeEventListener("visibilitychange", surVisibilite);
  }, [actif]);

  // Le thème clair en dépend : avec la vidéo, le Fond est assombri et le texte posé dessus devient clair (index.css).
  const avecVideo = courant !== undefined;
  useEffect(() => {
    if (!avecVideo) return;
    document.documentElement.dataset.fond = "video";
    return () => {
      delete document.documentElement.dataset.fond;
    };
  }, [avecVideo]);

  // Le clip suivant n'est préchargé qu'en métadonnées, hors de l'écran : un seul clip est chargé en entier.
  const suivantId = courant && lisibles.length > 1 ? lisibles[(rang + 1) % lisibles.length].id : undefined;
  useEffect(() => {
    if (suivantId === undefined) return;
    const precharge = document.createElement("video");
    precharge.preload = "metadata";
    precharge.muted = true;
    precharge.src = clipVideoUrl(suivantId);
    return () => {
      precharge.removeAttribute("src");
      precharge.load?.();
    };
  }, [suivantId]);

  const suivant = () => setRang((r) => r + 1);

  return (
    <>
      <div aria-hidden className="pointer-events-none fixed inset-0 -z-10 overflow-hidden">
        <div data-testid="fond-fixe" className="absolute inset-0 bg-background" />
        {courant && (
          <>
            <video
              key={courant.id}
              ref={video}
              src={clipVideoUrl(courant.id)}
              autoPlay
              muted
              playsInline
              loop={lisibles.length === 1}
              preload="metadata"
              onEnded={suivant}
              // Un clip illisible est sauté : le suivant prend sa place dans la liste.
              onError={() => setIllisibles((ids) => [...ids, courant.id])}
              className="absolute inset-0 size-full scale-105 object-cover blur-[12px]"
            />
            <div className="absolute inset-0 bg-black/55" />
          </>
        )}
      </div>
      {courant && (
        <div className="fixed right-4 bottom-4 z-10 flex items-center gap-3 rounded-full border bg-card px-4 py-2 text-xs text-card-foreground shadow-sm">
          <span>
            Fond : clip de <strong>{courant.auteurNom}</strong>
            {courant.gameId != null && (
              <>
                {" · "}
                <Link to="/games/$gameId" params={{ gameId: String(courant.gameId) }} className="underline-offset-2 hover:underline">
                  Game {courant.gameId}
                </Link>
              </>
            )}
          </span>
          <button type="button" onClick={suivant} className="font-bold">
            Suivant
          </button>
        </div>
      )}
    </>
  );
}
