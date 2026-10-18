import { PlayIcon } from "lucide-react";
import { useState } from "react";
import { $api } from "@/api/client";
import { InterrupteurDuFond } from "@/components/fil-de-clips";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { tailleLabel } from "@/lib/clips";
import { dateHeureLabel } from "@/lib/dates";

const TAILLE_DE_PAGE = 50;

/**
 * L'onglet Clips du Fond : tous les clips en vignettes avec l'interrupteur « Dans le Fond / Masqué »,
 * et un filtre pour ne voir que les masqués.
 */
export function ClipsAdmin() {
  const clips = $api.useInfiniteQuery(
    "get",
    "/clips",
    { params: { query: { taille: TAILLE_DE_PAGE } } },
    {
      initialPageParam: 1,
      pageParamName: "page",
      getNextPageParam: (page) => (page.page * page.taille < page.total ? page.page + 1 : undefined),
    },
  );
  const [seulementMasques, setSeulementMasques] = useState(false);

  if (clips.isPending) return <LoadingRows />;
  if (clips.isError) return <ErrorAlert error={clips.error} />;

  const tous = clips.data.pages.flatMap((page) => page.items);
  const affiches = seulementMasques ? tous.filter((c) => c.masqueDuFond) : tous;

  return (
    <div className="flex flex-col gap-4">
      <label className="flex items-center gap-2 text-sm">
        <Switch aria-label="Masqués" checked={seulementMasques} onCheckedChange={setSeulementMasques} />
        Masqués
      </label>
      {affiches.length === 0 ? (
        <p className="text-sm text-muted-foreground">{seulementMasques ? "Aucun clip masqué." : "Aucun clip pour l'instant."}</p>
      ) : (
        <ul className="grid grid-cols-2 gap-3 md:grid-cols-4">
          {affiches.map((clip) => (
            <li key={clip.id} className="flex flex-col gap-2 rounded-md border bg-card p-2">
              <span className="flex aspect-video w-full items-center justify-center rounded bg-muted text-muted-foreground">
                <PlayIcon className="size-6" />
              </span>
              <span className="truncate text-sm font-medium">{clip.nomFichier}</span>
              <span className="text-xs text-muted-foreground">
                {clip.auteurNom} · {tailleLabel(clip.taille)} · {dateHeureLabel(clip.publieLe)}
              </span>
              <InterrupteurDuFond clip={clip} />
            </li>
          ))}
        </ul>
      )}
      {clips.hasNextPage && (
        <Button variant="outline" className="self-center" disabled={clips.isFetchingNextPage} onClick={() => clips.fetchNextPage()}>
          Charger plus de clips
        </Button>
      )}
    </div>
  );
}
