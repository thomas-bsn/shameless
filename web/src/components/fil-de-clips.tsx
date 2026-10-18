import { ChevronLeftIcon, ChevronRightIcon, CircleCheckIcon, PlayIcon, VideoIcon } from "lucide-react";
import { useState } from "react";
import { Link } from "@tanstack/react-router";
import { $api, type Schemas } from "@/api/client";
import { ClipVideo } from "@/components/clip-video";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { RattacherClip, RetirerClip } from "@/components/rattachement-de-clip";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { dateHeureLabel } from "@/lib/dates";
import { grouperParSoiree, libelleDeGame, soireeLabel, tailleLabel } from "@/lib/clips";

type Clip = Schemas["ClipDeLaListe"];

const TAILLE_DE_PAGE = 50;
const TAILLE_A_RATTACHER = 200;

/** Ce qui est ouvert : le lecteur d'un clip, ou la fenêtre pour le rattacher. */
type Ouvert = { fenetre: "lecteur" | "rattacher"; clipId: number } | null;

/**
 * Le fil des Clips (ADR 0014) : en tête ceux sans Game, à rattacher, puis les autres par soirée et par
 * Game avec son score. Un clic ouvre le lecteur, qui enchaîne les clips dans l'ordre du fil.
 */
export function FilDeClips() {
  const sansGame = $api.useQuery("get", "/clips", {
    params: { query: { sansGame: true, taille: TAILLE_A_RATTACHER } },
  });
  const avecGame = $api.useInfiniteQuery(
    "get",
    "/clips",
    { params: { query: { sansGame: false, taille: TAILLE_DE_PAGE } } },
    {
      initialPageParam: 1,
      pageParamName: "page",
      getNextPageParam: (page) => (page.page * page.taille < page.total ? page.page + 1 : undefined),
    },
  );
  const [ouvert, setOuvert] = useState<Ouvert>(null);

  if (sansGame.isPending || avecGame.isPending) return <LoadingRows />;
  if (sansGame.isError) return <ErrorAlert error={sansGame.error} />;
  if (avecGame.isError) return <ErrorAlert error={avecGame.error} />;

  const aRattacher = sansGame.data.items;
  const rattaches = avecGame.data.pages.flatMap((page) => page.items);
  const soirees = grouperParSoiree(rattaches);
  // L'ordre du lecteur est celui de l'écran : À rattacher, puis le fil.
  const ordre = [...aRattacher, ...soirees.flatMap((s) => s.games.flatMap((g) => g.clips))];
  const clipOuvert = ouvert == null ? undefined : ordre.find((c) => c.id === ouvert.clipId);
  const ouvrir = (fenetre: "lecteur" | "rattacher", clip: Clip) => setOuvert({ fenetre, clipId: clip.id });
  const fermer = () => setOuvert(null);

  return (
    <div className="flex flex-col gap-8">
      {ordre.length === 0 ? (
        <EtatVide
          icone={<VideoIcon className="size-8" />}
          titre="Aucun clip pour l'instant"
          texte="Poste une vidéo dans le salon #clips de Discord. Elle apparaîtra ici, rangée dans sa game."
        />
      ) : (
        <>
          {aRattacher.length > 0 ? (
            <section aria-label={`À rattacher · ${sansGame.data.total} ${sansGame.data.total > 1 ? "clips" : "clip"}`}>
              <h2 className="text-lg font-semibold">
                À rattacher · {sansGame.data.total} {sansGame.data.total > 1 ? "clips" : "clip"}
              </h2>
              <p className="mb-3 text-sm text-muted-foreground">Shameless n'a pas su trouver la game.</p>
              <ul className="flex gap-3 overflow-x-auto pb-2">
                {aRattacher.map((clip) => (
                  <li key={clip.id} className="flex w-56 shrink-0 flex-col gap-2">
                    <Vignette clip={clip} onLire={() => ouvrir("lecteur", clip)} />
                    <Button size="sm" aria-label={`Rattacher ${clip.nomFichier}`} onClick={() => ouvrir("rattacher", clip)}>
                      Rattacher
                    </Button>
                    <p className="text-xs text-muted-foreground">{dateHeureLabel(clip.publieLe)}</p>
                  </li>
                ))}
              </ul>
            </section>
          ) : (
            <EtatVide
              icone={<CircleCheckIcon className="size-8" />}
              titre="Tout est rattaché"
              texte="Chaque clip a sa game. Les clips que Shameless ne sait pas placer apparaîtront ici."
            />
          )}

          {soirees.map((soiree) => (
            <section key={soiree.jour} aria-label={soireeLabel(soiree.jour)} className="flex flex-col gap-3">
              <h2 className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">{soireeLabel(soiree.jour)}</h2>
              {soiree.games.map(({ game, clips }) => (
                <Card key={game.gameId} role="group" aria-label={libelleDeGame(game)}>
                  <CardContent className="flex flex-col gap-3">
                    <div className="flex items-center justify-between gap-2">
                      <h3 className="font-medium">{libelleDeGame(game)}</h3>
                      <p className="text-sm">
                        Équipe A <span className="font-mono font-semibold tabular-nums">{game.scoreA} — {game.scoreB}</span> Équipe B
                      </p>
                    </div>
                    <ul className="grid grid-cols-2 gap-3 md:grid-cols-4">
                      {clips.map((clip) => (
                        <li key={clip.id} className="flex flex-col gap-1">
                          <Vignette clip={clip} onLire={() => ouvrir("lecteur", clip)} />
                          <p className="text-xs text-muted-foreground">{dateHeureLabel(clip.publieLe)}</p>
                        </li>
                      ))}
                    </ul>
                  </CardContent>
                </Card>
              ))}
            </section>
          ))}

          {avecGame.hasNextPage && (
            <Button variant="outline" className="self-center" disabled={avecGame.isFetchingNextPage} onClick={() => avecGame.fetchNextPage()}>
              Charger plus de clips
            </Button>
          )}
        </>
      )}

      {ouvert?.fenetre === "lecteur" && clipOuvert && (
        <Lecteur clip={clipOuvert} ordre={ordre} onChoisir={(clip) => ouvrir("lecteur", clip)} onChanger={() => ouvrir("rattacher", clipOuvert)} onFermer={fermer} />
      )}
      {ouvert?.fenetre === "rattacher" && clipOuvert && <FenetreDeRattachement clip={clipOuvert} onFermer={fermer} />}
    </div>
  );
}

function EtatVide({ icone, titre, texte }: { icone: React.ReactNode; titre: string; texte: string }) {
  return (
    <Card>
      <CardContent className="flex flex-col items-center gap-2 text-center">
        <span className="text-muted-foreground">{icone}</span>
        <h2 className="text-lg font-semibold">{titre}</h2>
        <p className="max-w-md text-sm text-muted-foreground">{texte}</p>
      </CardContent>
    </Card>
  );
}

function Vignette({ clip, onLire }: { clip: Clip; onLire: () => void }) {
  return (
    <button
      type="button"
      aria-label={`Lire le clip ${clip.nomFichier}`}
      onClick={onLire}
      className="flex flex-col gap-1 rounded-md border bg-card p-2 text-left hover:bg-accent"
    >
      <span className="flex aspect-video w-full items-center justify-center rounded bg-muted text-muted-foreground">
        <PlayIcon className="size-6" />
      </span>
      <span className="text-sm font-medium">{clip.auteurNom}</span>
      <span className="font-mono text-xs text-muted-foreground">{tailleLabel(clip.taille)}</span>
    </button>
  );
}

function Lecteur({
  clip,
  ordre,
  onChoisir,
  onChanger,
  onFermer,
}: {
  clip: Clip;
  ordre: Clip[];
  onChoisir: (clip: Clip) => void;
  onChanger: () => void;
  onFermer: () => void;
}) {
  const rang = ordre.findIndex((c) => c.id === clip.id);
  const precedent = ordre[rang - 1];
  const suivant = ordre[rang + 1];

  return (
    <Dialog open onOpenChange={(ouvert) => !ouvert && onFermer()}>
      <DialogContent className="sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>Clip de {clip.auteurNom}</DialogTitle>
          <DialogDescription>
            {[clip.game ? libelleDeGame(clip.game) : "Sans game", dateHeureLabel(clip.publieLe), tailleLabel(clip.taille)].join(" · ")}
          </DialogDescription>
        </DialogHeader>
        <ClipVideo key={clip.id} clipId={clip.id} />
        <DialogFooter className="justify-between">
          <Button size="icon" variant="outline" aria-label="Clip précédent" disabled={!precedent} onClick={() => precedent && onChoisir(precedent)}>
            <ChevronLeftIcon />
          </Button>
          <div className="flex flex-wrap items-center justify-center gap-2">
            {clip.gameId != null && (
              <Button size="sm" variant="ghost" asChild>
                <Link to="/games/$gameId" params={{ gameId: String(clip.gameId) }}>
                  Ouvrir la game
                </Link>
              </Button>
            )}
            <Button size="sm" variant="ghost" onClick={onChanger}>
              Changer de game
            </Button>
            {clip.gameId != null && <RetirerClip clip={clip} onRetire={onFermer} />}
          </div>
          <Button size="icon" variant="outline" aria-label="Clip suivant" disabled={!suivant} onClick={() => suivant && onChoisir(suivant)}>
            <ChevronRightIcon />
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function FenetreDeRattachement({ clip, onFermer }: { clip: Clip; onFermer: () => void }) {
  return (
    <Dialog open onOpenChange={(ouvert) => !ouvert && onFermer()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>À quelle game ce clip appartient-il ?</DialogTitle>
          <DialogDescription>
            Clip de {clip.auteurNom} · publié {dateHeureLabel(clip.publieLe)}
          </DialogDescription>
        </DialogHeader>
        <RattacherClip clip={clip} sauf={clip.gameId ?? undefined} onRattache={onFermer} />
      </DialogContent>
    </Dialog>
  );
}
