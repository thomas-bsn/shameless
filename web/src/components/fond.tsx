import { useEffect, useState } from "react";
import { $api } from "@/api/client";
import { Button } from "@/components/ui/button";
import { useQuery } from "@tanstack/react-query";
import { sessionQuery } from "@/lib/session";

/** Fond décoratif des pages de session ; le CSS garde le décor statique sur mobile et reduced motion. */
export function Fond() {
  const session = useQuery(sessionQuery);
  const clips = $api.useQuery("get", "/clips/fond", { params: { query: { take: 10 } }, enabled: !!session.data });
  const [index, setIndex] = useState(0);
  const [skipped, setSkipped] = useState<number[]>([]);
  const items = clips.data ?? [];
  const playlist = items.filter((item) => !skipped.includes(item.id));
  const clip = playlist.length ? playlist[index % playlist.length] : undefined;
  const next = () => setIndex((i) => (i + 1) % Math.max(playlist.length, 1));
  const skip = () => { if (clip) setSkipped((s) => [...s, clip.id]); setIndex(0); };
  useEffect(() => {
    const video = document.querySelector<HTMLVideoElement>(".fond-video");
    if (!video) return;
    const sync = () => document.hidden ? video.pause() : void video.play().catch(() => undefined);
    document.addEventListener("visibilitychange", sync);
    return () => document.removeEventListener("visibilitychange", sync);
  }, [clip?.id]);
  return <div className="fond">
    {clip && <video aria-hidden="true" key={clip.id} className="fond-video" autoPlay muted playsInline preload="metadata" src={`/api/clips/${clip.id}/fichier`} onEnded={next} onError={skip} />}
    {clip && <div className="fond-credit"><span>{clip.auteurNom}{clip.gameId != null ? ` · Game #${clip.gameId}` : ""}</span><Button type="button" size="sm" variant="outline" aria-label="Clip suivant" onClick={next}>Suivant</Button></div>}
  </div>;
}
