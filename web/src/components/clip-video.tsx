/** L'URL de la vidéo d'un clip, servie par l'API (même origine : le cookie de session suffit). */
export const clipVideoUrl = (clipId: number) => `/api/clips/${clipId}/fichier`;

/** La vidéo d'un clip, lisible sur place (ADR 0014). */
export function ClipVideo({ clipId }: { clipId: number }) {
  return <video controls preload="metadata" src={clipVideoUrl(clipId)} className="aspect-video w-full rounded-md bg-black" />;
}
