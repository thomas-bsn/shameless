import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute } from "@tanstack/react-router";
import { $api, type Schemas } from "@/api/client";
import { PageHeader } from "@/components/page-header";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { sessionQuery } from "@/lib/session";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";

export const Route = createFileRoute("/_connecte/administration")({ component: Administration });

function Administration() {
  const session = useQuery(sessionQuery);
  const [tab, setTab] = useState<"joueurs" | "clips">("joueurs");
  if (session.isPending) return <LoadingRows />;
  if (!session.data?.isAdmin) return <><PageHeader title="Administration" /><p role="alert">Cette page est réservée aux Administrateurs.</p></>;
  return <div className="flex max-w-5xl flex-col gap-6">
    <PageHeader title="Administration" description="Les actions réservées aux Administrateurs." />
    <Atraiter />
    <div role="tablist" aria-label="Administration" className="flex gap-2">
      <Button role="tab" aria-selected={tab === "joueurs"} variant={tab === "joueurs" ? "default" : "outline"} onClick={() => setTab("joueurs")}>Joueurs</Button>
      <Button role="tab" aria-selected={tab === "clips"} variant={tab === "clips" ? "default" : "outline"} onClick={() => setTab("clips")}>Clips du Fond</Button>
    </div>
    {tab === "joueurs" ? <JoueursAdmin /> : <ClipsAdmin />}
  </div>;
}

function Atraiter() {
  const qc = useQueryClient();
  const pending = $api.useQuery("get", "/games/pending");
  const active = $api.useQuery("get", "/series/active");
  const winner = $api.useMutation("put", "/games/{id}/result", { onSuccess: () => qc.invalidateQueries() });
  const stop = $api.useMutation("post", "/series/{id}/stop", { onSuccess: () => qc.invalidateQueries() });
  if (pending.isPending || active.isPending) return <LoadingRows />;
  if (pending.isError) return <ErrorAlert error={pending.error} />;
  if (active.isError) return <ErrorAlert error={active.error} />;
  const games = pending.data;
  const series = active.data.series ?? [];
  if (!games.length && !series.length) return <Card><CardContent className="py-5 text-sm text-muted-foreground">Rien à traiter. Tout est en ordre.</CardContent></Card>;
  return <section className="flex flex-col gap-3"><h2 className="text-lg font-semibold">À traiter</h2>
    {games.map((g) => <Card key={`g${g.id}`}><CardContent className="flex flex-wrap items-center justify-between gap-3 py-4"><span>Game #{g.id} · {g.participants.map((p) => p.prenom).join(", ")}</span><span className="flex gap-2"><Button disabled={winner.isPending} onClick={() => winner.mutate({ params: { path: { id: g.id } }, body: { winner: "A" } })}>Équipe A gagne</Button><Button variant="outline" disabled={winner.isPending} onClick={() => winner.mutate({ params: { path: { id: g.id } }, body: { winner: "B" } })}>Équipe B gagne</Button></span></CardContent></Card>)}
    {series.map((s) => <Card key={`s${s.seriesId}`}><CardContent className="flex items-center justify-between gap-3 py-4"><span>Série #{s.seriesId} · en cours</span><Button variant="destructive" disabled={stop.isPending} onClick={() => { if (window.confirm("Interrompre cette série ? Les games déjà jouées restent dans l'historique.")) stop.mutate({ params: { path: { id: s.seriesId } } }); }}>Interrompre</Button></CardContent></Card>)}
    {winner.isError && <ErrorAlert error={winner.error} />}{stop.isError && <ErrorAlert error={stop.error} />}
  </section>;
}

function JoueursAdmin() {
  const players = $api.useQuery("get", "/players");
  if (players.isPending) return <LoadingRows />;
  if (players.isError) return <ErrorAlert error={players.error} />;
  return <div className="flex flex-col gap-2">{players.data.map((p) => <JoueurAdminRow key={p.id} player={p} />)}</div>;
}

function JoueurAdminRow({ player: p }: { player: Schemas["PlayerDto"] }) {
  const qc = useQueryClient();
  const [value, setValue] = useState(p.currentCost);
  const details = $api.useQuery("get", "/players/{playerId}/cost", { params: { path: { playerId: p.id } } });
  const cost = $api.useMutation("put", "/players/{playerId}/cost", { onSuccess: () => qc.invalidateQueries() });
  const peak = $api.useMutation("post", "/players/{playerId}/peak", { onSuccess: () => qc.invalidateQueries() });
  const clearPeak = $api.useMutation("delete", "/players/{playerId}/peak", { onSuccess: () => qc.invalidateQueries() });
  const source = details.data?.sourceDuCostDeDepart;
  const delta = (n: number) => setValue((v) => Math.max(1, Math.min(6, Math.round((v + n) * 10) / 10)));
  return <Card><CardContent className="flex flex-wrap items-center gap-3 py-3"><span className="min-w-32 flex-1 font-medium">{p.prenom}</span><span className="text-xs text-muted-foreground">{source === "FixeParAdmin" ? "Fixé par un admin" : source === "RangSoloQ" ? "Calculé depuis SoloQ" : source === "MoisPrecedent" ? "Cost du mois précédent" : "Cost calculé"}</span>
    <Button variant="outline" onClick={() => delta(-0.1)}>−</Button><Input aria-label={`Cost de ${p.prenom}`} className="w-20" type="number" min="1" max="6" step="0.1" value={value} onChange={(e) => setValue(Number(e.target.value))} /><Button variant="outline" onClick={() => delta(0.1)}>+</Button>
    <Button disabled={cost.isPending} onClick={() => cost.mutate({ params: { path: { playerId: p.id } }, body: { cost: value } })}>Enregistrer</Button>
    <Input aria-label={`Peak de ${p.prenom}`} placeholder="Peak (tier)" className="w-28" onBlur={(e) => { const tier = e.target.value.trim(); if (tier) peak.mutate({ params: { path: { playerId: p.id } }, body: { peakTier: tier, peakDivision: null, peakSeason: "", peakLp: 0 } }); }} /><Button variant="outline" onClick={() => clearPeak.mutate({ params: { path: { playerId: p.id } } })}>Effacer peak</Button>
  </CardContent></Card>;
}

function ClipsAdmin() {
  const qc = useQueryClient();
  const [masked, setMasked] = useState(false);
  const clips = $api.useQuery("get", "/clips", { params: { query: { masques: masked, sansGame: false, page: 1, pageSize: 100 } } });
  const set = $api.useMutation("put", "/clips/{id}/fond", { onSuccess: () => qc.invalidateQueries() });
  if (clips.isPending) return <LoadingRows />;
  if (clips.isError) return <ErrorAlert error={clips.error} />;
  return <section className="flex flex-col gap-4"><label className="flex items-center gap-2"><input type="checkbox" checked={masked} onChange={(e) => setMasked(e.target.checked)} /> Masqués seulement</label><div className="grid gap-3 sm:grid-cols-2">{clips.data.map((c) => <Card key={c.id}><CardContent className="flex items-center justify-between gap-3 py-4"><span className="truncate">{c.auteurNom} · #{c.id}</span><Button variant="outline" disabled={set.isPending} onClick={() => set.mutate({ params: { path: { id: c.id } }, body: { masque: !c.masqueDuFond } })}>{c.masqueDuFond ? "Réafficher" : "Masquer"}</Button></CardContent></Card>)}</div></section>;
}
