import { useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { ClipVideo } from "@/components/clip-video";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { RattacherClip, RetirerClip } from "@/components/rattachement-de-clip";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { dateHeureLabel } from "@/lib/dates";

/** Les Clips rattachés à une Game, lisibles sur place (ADR 0014). */
export function GameClips({ gameId }: { gameId: number }) {
  const clips = $api.useQuery("get", "/games/{gameId}/clips", { params: { path: { gameId } } });

  return (
    <Card role="region" aria-labelledby="clips-de-la-game">
      <CardHeader>
        <CardTitle id="clips-de-la-game" className="text-sm">
          Clips
        </CardTitle>
      </CardHeader>
      <CardContent>
        {clips.isPending ? (
          <LoadingRows />
        ) : clips.isError ? (
          <ErrorAlert error={clips.error} />
        ) : clips.data.length === 0 ? (
          <p className="text-sm text-muted-foreground">Aucun clip pour cette Game : ceux de #clips y apparaissent.</p>
        ) : (
          <ul className="grid gap-4 md:grid-cols-2">
            {clips.data.map((clip) => (
              <li key={clip.id}>
                <Clip clip={clip} />
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

function Clip({ clip }: { clip: Schemas["ClipResponse"] }) {
  const [corriger, setCorriger] = useState(false);

  return (
    <figure aria-label={clip.nomFichier} className="flex flex-col gap-2">
      <ClipVideo clipId={clip.id} />
      <figcaption className="text-xs text-muted-foreground">
        {clip.auteurNom} · {dateHeureLabel(clip.publieLe)}
        {clip.rattachement === "manuel" && " · rattaché à la main"}
      </figcaption>
      <div className="flex flex-wrap gap-1">
        <Button size="sm" variant="ghost" aria-expanded={corriger} onClick={() => setCorriger((c) => !c)}>
          Corriger la Game
        </Button>
        <RetirerClip clip={clip} />
      </div>
      {corriger && clip.gameId != null && <RattacherClip clip={clip} sauf={clip.gameId} />}
    </figure>
  );
}
