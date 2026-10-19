import { PlayIcon } from "lucide-react";
import { useState } from "react";

/** L'URL de la vidéo d'un clip, servie par l'API (même origine : le cookie de session suffit). */
export const clipVideoUrl = (clipId: number) => `/api/clips/${clipId}/fichier`;

/** La vidéo d'un clip, lisible sur place (ADR 0014). */
export function ClipVideo({ clipId }: { clipId: number }) {
  return <video controls preload="metadata" src={clipVideoUrl(clipId)} className="aspect-video w-full rounded-md bg-black" />;
}

/**
 * Miniature d'un clip : la première image de la vidéo (preload metadata + `#t=0.001`, muted et
 * playsInline pour Safari/iOS). L'icône de lecture reste dessous, visible tant que l'image n'est
 * pas chargée ou si la vidéo est en erreur.
 */
export function ClipMiniature({ clipId }: { clipId: number }) {
  const [erreur, setErreur] = useState(false);
  return (
    <span className="relative flex aspect-video w-full items-center justify-center overflow-hidden rounded bg-muted text-muted-foreground">
      <PlayIcon className="size-6" />
      {!erreur && (
        <video
          preload="metadata"
          muted
          playsInline
          tabIndex={-1}
          aria-hidden="true"
          src={`${clipVideoUrl(clipId)}#t=0.001`}
          onError={() => setErreur(true)}
          className="pointer-events-none absolute inset-0 size-full bg-black object-cover"
        />
      )}
    </span>
  );
}
