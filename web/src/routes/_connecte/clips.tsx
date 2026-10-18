import { createFileRoute } from "@tanstack/react-router";
import { $api, type Schemas } from "@/api/client";
import { ClipVideo } from "@/components/clip-video";
import { PageHeader } from "@/components/page-header";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { RattacherClip } from "@/components/rattachement-de-clip";
import { Card, CardContent } from "@/components/ui/card";
import { dateHeureLabel } from "@/lib/dates";

/** Les clips de #clips sans Game (aucune ou plusieurs Games plausibles), à rattacher à la main (ADR 0014). */
export const Route = createFileRoute("/_connecte/clips")({
  component: ClipsARattacher,
});

function ClipsARattacher() {
  const clips = $api.useQuery("get", "/clips/sans-game");

  return (
    <>
      <PageHeader
        title="Clips à rattacher"
        description="Les clips de #clips que Shameless n'a pas su rattacher à une Game : choisis la bonne."
      />
      {clips.isPending ? (
        <LoadingRows />
      ) : clips.isError ? (
        <ErrorAlert error={clips.error} />
      ) : clips.data.length === 0 ? (
        <p className="text-sm text-muted-foreground">Aucun clip à rattacher : chaque clip a sa Game.</p>
      ) : (
        <ul className="grid gap-4 md:grid-cols-2">
          {clips.data.map((clip) => (
            <li key={clip.id}>
              <ClipSansGame clip={clip} />
            </li>
          ))}
        </ul>
      )}
    </>
  );
}

function ClipSansGame({ clip }: { clip: Schemas["ClipResponse"] }) {
  return (
    <Card role="article" aria-label={clip.nomFichier}>
      <CardContent className="flex flex-col gap-3">
        <ClipVideo clipId={clip.id} />
        <p className="text-xs text-muted-foreground">
          {clip.auteurNom} · {dateHeureLabel(clip.publieLe)}
        </p>
        <RattacherClip clip={clip} />
      </CardContent>
    </Card>
  );
}
