import { useMemo, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link } from "@tanstack/react-router";
import { $api, type Schemas } from "@/api/client";
import { ClipVideo } from "@/components/clip-video";
import { PageHeader } from "@/components/page-header";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { RattacherClip, RetirerClip } from "@/components/rattachement-de-clip";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Switch } from "@/components/ui/switch";
import { sessionQuery } from "@/lib/session";
import { dateHeureLabel } from "@/lib/dates";

export const Route = createFileRoute("/_connecte/clips")({ component: Clips });

const EMPTY_CLIPS: Schemas["ClipResponse"][] = [];

function Clips() {
  const [sansGame, setSansGame] = useState(false);
  const [auteur, setAuteur] = useState("");
  const [soireeId, setSoireeId] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<number | null>(null);
  const session = useQuery(sessionQuery);
  const clips = $api.useQuery("get", "/clips", { params: { query: { sansGame, auteur: auteur || undefined, soireeId: soireeId ? Number(soireeId) : undefined, page, pageSize: 30 } } });
  const items = clips.data ?? EMPTY_CLIPS;
  const selectedIndex = items.findIndex((c) => c.id === selected);
  const groups = useMemo(() => {
    const group = new Map<string, Map<string, Schemas["ClipResponse"][]>>();
    for (const clip of items) {
      const soiree = clip.gameId == null ? "À rattacher" : clip.soireeId == null ? "Games sans Soirée" : `Soirée #${clip.soireeId}`;
      const game = clip.gameId == null ? "À rattacher" : `Game #${clip.gameId}${clip.scoreEquipeA != null && clip.scoreEquipeB != null ? ` · ${clip.scoreEquipeA}–${clip.scoreEquipeB}` : ""}`;
      const games = group.get(soiree) ?? new Map<string, Schemas["ClipResponse"][]>();
      games.set(game, [...(games.get(game) ?? []), clip]);
      group.set(soiree, games);
    }
    return [...group.entries()];
  }, [items]);

  return <div className="flex max-w-6xl flex-col gap-6">
    <PageHeader title="Clips" description="Les moments partagés dans #clips, classés par partie." />
    <div className="flex flex-wrap items-center gap-3"><label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={sansGame} onChange={(e) => { setSansGame(e.target.checked); setPage(1); }} /> À rattacher seulement</label><input aria-label="Filtrer par auteur" placeholder="Auteur" className="h-9 w-40 rounded-md border border-input bg-background px-3 text-sm" value={auteur} onChange={(e) => { setAuteur(e.target.value); setPage(1); }} /><input aria-label="Filtrer par soirée" placeholder="N° de soirée" inputMode="numeric" className="h-9 w-32 rounded-md border border-input bg-background px-3 text-sm" value={soireeId} onChange={(e) => { setSoireeId(e.target.value.replace(/\D/g, "")); setPage(1); }} /></div>
    {clips.isPending ? <LoadingRows /> : clips.isError ? <ErrorAlert error={clips.error} /> : items.length === 0 ? <p className="py-12 text-center text-muted-foreground">{page > 1 ? "Fin des clips." : sansGame ? "Tout est rattaché." : "Aucun clip pour l'instant. Publie une vidéo dans #clips pour la retrouver ici."}</p> : <>
      {groups.map(([soiree, games]) => <section key={soiree} aria-label={soiree} className="flex flex-col gap-3"><h2 className="text-lg font-semibold">{soiree}</h2>{[...games.entries()].map(([heading, entries]) => <div key={heading} className="flex flex-col gap-3"><h3 className="text-base font-medium">{heading}</h3><div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{entries.map((c) => <Card key={c.id} role="article" aria-label={c.nomFichier} className="overflow-hidden"><CardContent className="flex flex-col gap-3 p-3"><button className="text-left" onClick={() => setSelected(c.id)} aria-label={`Lire le clip de ${c.auteurNom}`}><ClipVideo clipId={c.id} /></button><div className="flex items-center justify-between gap-2 text-xs text-muted-foreground"><span className="truncate">{c.auteurNom}</span><span>{dateHeureLabel(c.publieLe)}</span></div>{c.gameId != null && <Link className="text-sm underline" to="/games/$gameId" params={{ gameId: String(c.gameId) }}>Ouvrir la Game #{c.gameId}</Link>}{c.gameId == null && <RattacherClip clip={c} />}{session.data?.isAdmin && <FondToggle clip={c} />}</CardContent></Card>)}</div></div>)}</section>)}
      <div className="flex justify-center gap-2"><Button variant="outline" disabled={page === 1} onClick={() => setPage((p) => p - 1)}>Précédent</Button><span className="self-center text-sm">Page {page}</span><Button variant="outline" disabled={items.length < 30} onClick={() => setPage((p) => p + 1)}>Suivant</Button></div>
    </>}
    {selectedIndex >= 0 && <ClipViewer clips={items} index={selectedIndex} onClose={() => setSelected(null)} onSelect={(id) => setSelected(id)} isAdmin={!!session.data?.isAdmin} />}
  </div>;
}

function FondToggle({ clip }: { clip: Schemas["ClipResponse"] }) {
  const queryClient = useQueryClient();
  const mutation = $api.useMutation("put", "/clips/{id}/fond", {
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["get", "/clips"] }),
  });
  const label = clip.masqueDuFond ? "Masqué du Fond" : "Dans le Fond";
  return <label className="flex items-center justify-between gap-2 border-t pt-3 text-sm">
    <span>{label}</span>
    <Switch
      checked={!clip.masqueDuFond}
      aria-label="Dans le Fond"
      disabled={mutation.isPending}
      onCheckedChange={(checked) => mutation.mutate({ params: { path: { id: clip.id } }, body: { masque: !checked } })}
    />
  </label>;
}

function ClipViewer({ clips, index, onClose, onSelect, isAdmin }: { clips: Schemas["ClipResponse"][]; index: number; onClose: () => void; onSelect: (id: number) => void; isAdmin: boolean }) {
  const qc = useQueryClient();
  const clip = clips[index]!;
  const setFond = $api.useMutation("put", "/clips/{id}/fond", { onSuccess: () => qc.invalidateQueries() });
  return <div role="dialog" aria-modal="true" aria-label="Lecture du clip" className="fixed inset-0 z-[70] grid place-items-center bg-black/80 p-4" onMouseDown={(e) => { if (e.target === e.currentTarget) onClose(); }}>
    <div className="w-full max-w-4xl rounded-xl border border-white/20 bg-background p-4 text-foreground shadow-2xl"><div className="mb-3 flex items-center justify-between"><strong>{clip.auteurNom} · {dateHeureLabel(clip.publieLe)}</strong><Button variant="outline" onClick={onClose}>Fermer</Button></div><ClipVideo clipId={clip.id} /><div className="mt-4 flex flex-wrap items-center justify-between gap-2"><Button variant="outline" disabled={index <= 0} onClick={() => onSelect(clips[index - 1]!.id)}>Précédent</Button><div className="flex flex-wrap gap-2">{clip.gameId != null && <><Button asChild variant="outline"><Link to="/games/$gameId" params={{ gameId: String(clip.gameId) }}>Ouvrir la Game</Link></Button><RattacherClip clip={clip} sauf={clip.gameId} /><RetirerClip clip={clip} /></>}{clip.gameId == null && <RattacherClip clip={clip} />}{isAdmin && <Button variant="outline" disabled={setFond.isPending} onClick={() => setFond.mutate({ params: { path: { id: clip.id } }, body: { masque: !clip.masqueDuFond } })}>{clip.masqueDuFond ? "Réafficher dans le Fond" : "Masquer du Fond"}</Button>}</div><Button variant="outline" disabled={index >= clips.length - 1} onClick={() => onSelect(clips[index + 1]!.id)}>Suivant</Button></div></div>
  </div>;
}
