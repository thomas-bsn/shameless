import { useQueryClient } from "@tanstack/react-query";
import { ChevronLeft, ChevronRight, Trash2Icon, Undo2Icon } from "lucide-react";
import { useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { dateLabel } from "@/lib/dates";
import { type Month, monthLabel, shiftMonth } from "@/lib/history";
import { cn } from "@/lib/utils";

type Recap = Schemas["GameRecapResponse"];
type Participant = Recap["teamA"][number];
type Joueur = Schemas["PlayerDto"];

const currentMonth = (): Month => {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
};

const CHAMP_NATIF = "h-9 rounded-md border border-input bg-background px-2 text-sm shadow-xs";

/** Après une action : relire la liste du mois et la game. */
function useRelire() {
  const queryClient = useQueryClient();
  return () =>
    queryClient.invalidateQueries({
      predicate: (q) => typeof q.queryKey[1] === "string" && q.queryKey[1].startsWith("/games"),
    });
}

function ModifieeParAdmin({ le, className }: { le: string | null | undefined; className?: string }) {
  if (!le) return null;
  return <span className={cn("text-xs text-muted-foreground", className)}>modifiée par un admin le {dateLabel(le)}</span>;
}

/** L'onglet Games : les games terminées d'un mois, à corriger ou supprimer (ADR 0009). */
export function GamesAdmin() {
  const [mois, setMois] = useState<Month>(currentMonth);
  const [ouverte, setOuverte] = useState<number | null>(null);
  const maintenant = currentMonth();
  const suivant = shiftMonth(mois, 1);
  const aSuivant = suivant.year * 12 + suivant.month <= maintenant.year * 12 + maintenant.month;
  const historique = $api.useQuery("get", "/games/history/{year}/{month}", {
    params: { path: { year: mois.year, month: mois.month } },
  });

  const games = (historique.data?.games ?? []).flatMap((s) => s.games).filter((g) => g.winner === "A" || g.winner === "B");

  return (
    <Card>
      <CardHeader>
        <CardTitle>Games</CardTitle>
        <CardDescription>
          Corrigez le vainqueur, la composition ou les stats d'une game terminée, ou supprimez-la. Les données du Companion ne sont jamais
          modifiées : une correction se pose par-dessus et s'annule.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <nav aria-label="Mois" className="flex items-center gap-2">
          <Button variant="outline" size="icon" aria-label="Mois précédent" onClick={() => setMois(shiftMonth(mois, -1))}>
            <ChevronLeft />
          </Button>
          <span className="min-w-36 text-center font-medium">{monthLabel(mois)}</span>
          <Button variant="outline" size="icon" aria-label="Mois suivant" disabled={!aSuivant} onClick={() => setMois(suivant)}>
            <ChevronRight />
          </Button>
        </nav>
        {historique.isPending ? (
          <LoadingRows />
        ) : historique.isError ? (
          <ErrorAlert error={historique.error} />
        ) : games.length === 0 ? (
          <p className="text-sm text-muted-foreground">Aucune game terminée ce mois-ci.</p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Game</TableHead>
                <TableHead>Jouée le</TableHead>
                <TableHead>Vainqueur</TableHead>
                <TableHead>Action</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {games.map((g) => (
                <TableRow key={g.id}>
                  <TableCell className="font-medium">#{g.id}</TableCell>
                  <TableCell>{dateLabel(g.playedAt)}</TableCell>
                  <TableCell className="whitespace-normal">
                    <div className="flex flex-col items-start gap-1">
                      <span>Équipe {g.winner}</span>
                      <ModifieeParAdmin le={g.modifieeParAdminLe} />
                    </div>
                  </TableCell>
                  <TableCell>
                    <Button variant="outline" size="sm" aria-label={`Ouvrir la game #${g.id}`} onClick={() => setOuverte(g.id)}>
                      Corriger
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
      {ouverte !== null && <GameDialog gameId={ouverte} onFermer={() => setOuverte(null)} />}
    </Card>
  );
}

function GameDialog({ gameId, onFermer }: { gameId: number; onFermer: () => void }) {
  const game = $api.useQuery("get", "/games/{id}", { params: { path: { id: gameId } } });
  const [erreur, setErreur] = useState<unknown>(null);

  return (
    <Dialog open onOpenChange={(ouvert) => !ouvert && onFermer()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>Game #{gameId}</DialogTitle>
          <DialogDescription>
            {game.data ? <ModifieeParAdmin le={game.data.modifieeParAdminLe} /> : "Corriger les données de la game."}
          </DialogDescription>
        </DialogHeader>
        {erreur != null && <ErrorAlert error={erreur} />}
        {game.isPending ? (
          <LoadingRows />
        ) : game.isError ? (
          <ErrorAlert error={game.error} />
        ) : (
          <div className="flex flex-col gap-6">
            <Vainqueur game={game.data} onErreur={setErreur} />
            <Composition game={game.data} onErreur={setErreur} />
            <StatsDesJoueurs game={game.data} onErreur={setErreur} />
            <ImportDeRapport gameId={gameId} onErreur={setErreur} />
            <Suppression gameId={gameId} onErreur={setErreur} onSupprimee={onFermer} />
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

type SurErreur = { onErreur: (e: unknown) => void };

function Vainqueur({ game, onErreur }: { game: Recap } & SurErreur) {
  const relire = useRelire();
  const corriger = $api.useMutation("put", "/games/{id}/correction", {
    onMutate: () => onErreur(null),
    onError: onErreur,
    onSuccess: relire,
  });
  return (
    <section className="flex flex-col gap-2">
      <h3 className="font-semibold">Vainqueur</h3>
      <div className="flex flex-wrap items-center gap-2">
        {(["A", "B"] as const).map((equipe) => (
          <Button
            key={equipe}
            variant={game.winner === equipe ? "default" : "outline"}
            size="sm"
            aria-label={game.winner === equipe ? `Équipe ${equipe} (vainqueur actuel)` : `Donner la victoire à l'Équipe ${equipe}`}
            disabled={game.winner === equipe || corriger.isPending}
            onClick={() => corriger.mutate({ params: { path: { id: game.id } }, body: { winner: equipe, teamA: null, teamB: null } })}
          >
            Équipe {equipe}
          </Button>
        ))}
      </div>
    </section>
  );
}

const EQUIPE_TAILLE = 5;

function Composition({ game, onErreur }: { game: Recap } & SurErreur) {
  const ids = (equipe: Participant[]) => equipe.map((p) => p.playerId);
  return <CompositionForm key={JSON.stringify([ids(game.teamA), ids(game.teamB)])} game={game} onErreur={onErreur} />;
}

function CompositionForm({ game, onErreur }: { game: Recap } & SurErreur) {
  const relire = useRelire();
  const joueurs = $api.useQuery("get", "/players");
  const [equipes, setEquipes] = useState<{ A: number[]; B: number[] }>({
    A: game.teamA.map((p) => p.playerId),
    B: game.teamB.map((p) => p.playerId),
  });
  const [refus, setRefus] = useState<string | null>(null);
  const corriger = $api.useMutation("put", "/games/{id}/correction", {
    onMutate: () => onErreur(null),
    onError: onErreur,
    onSuccess: relire,
  });

  const choisir = (equipe: "A" | "B", index: number, id: number) =>
    setEquipes((e) => ({ ...e, [equipe]: e[equipe].map((v, i) => (i === index ? id : v)) }));

  const enregistrer = () => {
    const valide = (ids: number[]) => ids.length === EQUIPE_TAILLE && new Set(ids).size === EQUIPE_TAILLE;
    if (!valide(equipes.A) || !valide(equipes.B)) {
      setRefus("Chaque équipe doit avoir 5 joueurs distincts.");
      return;
    }
    setRefus(null);
    corriger.mutate({ params: { path: { id: game.id } }, body: { winner: null, teamA: equipes.A, teamB: equipes.B } });
  };

  const options: Joueur[] = joueurs.data ?? [];
  return (
    <section className="flex flex-col gap-3">
      <h3 className="font-semibold">Composition</h3>
      {joueurs.isError && <ErrorAlert error={joueurs.error} />}
      <div className="grid gap-4 sm:grid-cols-2">
        {(["A", "B"] as const).map((equipe) => (
          <div key={equipe} className="flex flex-col gap-2">
            <span className="text-sm font-medium">Équipe {equipe}</span>
            {equipes[equipe].map((id, i) => (
              <select
                key={i}
                aria-label={`Équipe ${equipe}, joueur ${i + 1}`}
                className={CHAMP_NATIF}
                value={id}
                onChange={(e) => choisir(equipe, i, Number(e.target.value))}
              >
                {!options.some((j) => j.id === id) && <option value={id}>Joueur #{id}</option>}
                {options.map((j) => (
                  <option key={j.id} value={j.id}>
                    {j.prenom}
                  </option>
                ))}
              </select>
            ))}
          </div>
        ))}
      </div>
      {refus && <p className="text-sm text-destructive">{refus}</p>}
      <div>
        <Button size="sm" disabled={corriger.isPending} onClick={enregistrer}>
          Enregistrer la composition
        </Button>
      </div>
    </section>
  );
}

const VOIES = ["Top", "Jungle", "Mid", "Adc", "Support"];

type ChampStat = "champion" | "position" | "kills" | "deaths" | "assists" | "cs" | "goldEarned" | "damageToChampions" | "visionScore" | "note";

const CHAMPS: { nom: ChampStat; label: string; texte?: boolean }[] = [
  { nom: "champion", label: "Champion", texte: true },
  { nom: "position", label: "Voie", texte: true },
  { nom: "kills", label: "Kills" },
  { nom: "deaths", label: "Morts" },
  { nom: "assists", label: "Assistances" },
  { nom: "cs", label: "CS" },
  { nom: "goldEarned", label: "Or" },
  { nom: "damageToChampions", label: "Dégâts aux champions" },
  { nom: "visionScore", label: "Vision" },
  { nom: "note", label: "Note" },
];

/** Les valeurs affichées d'un participant, sous forme de texte de formulaire. */
function valeursDe(p: Participant): Record<ChampStat, string> {
  const s = p.stats;
  const texte = (n: number | null | undefined) => (n == null ? "" : String(n));
  const cs = s && (s.minionsKilled != null || s.neutralMinionsKilled != null) ? (s.minionsKilled ?? 0) + (s.neutralMinionsKilled ?? 0) : null;
  return {
    champion: p.championId ?? "",
    position: s?.position ?? "",
    kills: texte(s?.kills),
    deaths: texte(s?.deaths),
    assists: texte(s?.assists),
    cs: texte(cs),
    goldEarned: texte(s?.goldEarned),
    damageToChampions: texte(s?.damageToChampions),
    visionScore: texte(s?.visionScore),
    note: texte(p.performance?.note),
  };
}

function StatsDesJoueurs({ game, onErreur }: { game: Recap } & SurErreur) {
  return (
    <section className="flex flex-col gap-3">
      <h3 className="font-semibold">Stats des joueurs</h3>
      {(["A", "B"] as const).map((equipe) => (
        <div key={equipe} className="flex flex-col gap-3">
          <span className="text-sm font-medium">Équipe {equipe}</span>
          {(equipe === "A" ? game.teamA : game.teamB).map((p) => (
            <StatsForm key={`${p.playerId}-${JSON.stringify(valeursDe(p))}-${p.champsCorriges.join()}`} gameId={game.id} participant={p} onErreur={onErreur} />
          ))}
        </div>
      ))}
    </section>
  );
}

const CORPS_VIDE: Schemas["CorrectPlayerStatsRequest"] = {
  champion: null,
  position: null,
  kills: null,
  deaths: null,
  assists: null,
  cs: null,
  goldEarned: null,
  damageToChampions: null,
  visionScore: null,
  note: null,
  champLevel: null,
  spell1Id: null,
  spell2Id: null,
  largestKillingSpree: null,
  killingSprees: null,
  doubleKills: null,
  tripleKills: null,
  quadraKills: null,
  pentaKills: null,
  minionsKilled: null,
  neutralMinionsKilled: null,
  goldSpent: null,
  physicalDamageToChampions: null,
  magicDamageToChampions: null,
  trueDamageToChampions: null,
  damageTaken: null,
  totalHeal: null,
  damageShielded: null,
  timeCcingOthers: null,
  wardsPlaced: null,
  wardsKilled: null,
  controlWardsBought: null,
  damageToObjectives: null,
  turretKills: null,
  dragonKills: null,
  baronKills: null,
  inhibitorKills: null,
  roleBoundItem: null,
  perkPrimaryStyle: null,
  perkSubStyle: null,
  item0: null,
  item1: null,
  item2: null,
  item3: null,
  item4: null,
  item5: null,
  item6: null,
  perk0: null,
  perk1: null,
  perk2: null,
  perk3: null,
  perk4: null,
  perk5: null,
};

function StatsForm({ gameId, participant, onErreur }: { gameId: number; participant: Participant } & SurErreur) {
  const relire = useRelire();
  const initiales = valeursDe(participant);
  const [valeurs, setValeurs] = useState(initiales);
  const options = { onMutate: () => onErreur(null), onError: onErreur, onSuccess: relire };
  const corriger = $api.useMutation("put", "/games/{id}/players/{playerId}/corrections", options);
  const annuler = $api.useMutation("delete", "/games/{id}/players/{playerId}/corrections/{champ}", options);
  const path = { id: gameId, playerId: participant.playerId };

  const modifies = CHAMPS.filter((c) => valeurs[c.nom] !== initiales[c.nom] && valeurs[c.nom].trim() !== "");
  const enregistrer = () => {
    const corps: Schemas["CorrectPlayerStatsRequest"] = { ...CORPS_VIDE };
    for (const c of modifies) {
      (corps as Record<ChampStat, string | number | null>)[c.nom] = c.texte ? valeurs[c.nom].trim() : Number(valeurs[c.nom]);
    }
    corriger.mutate({ params: { path }, body: corps });
  };

  return (
    <div className="flex flex-col gap-2 rounded-md border p-3">
      <span className="font-medium">{participant.prenom}</span>
      <div className="grid gap-3 sm:grid-cols-3">
        {CHAMPS.map((c) => {
          const nom = `${c.label} de ${participant.prenom}`;
          const corrige = participant.champsCorriges.includes(c.nom);
          return (
            <div key={c.nom} className="flex flex-col gap-1">
              <div className="flex items-center justify-between gap-1">
                <Label htmlFor={`${participant.playerId}-${c.nom}`} className="text-xs">
                  {c.label}
                  <span className="sr-only"> de {participant.prenom}</span>
                </Label>
                {corrige && (
                  <span className="flex items-center gap-1">
                    <span className="sr-only">{nom} : corrigé</span>
                    <Badge variant="secondary" aria-hidden>
                      corrigé
                    </Badge>
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      aria-label={`Annuler la correction de ${nom}`}
                      disabled={annuler.isPending}
                      onClick={() => annuler.mutate({ params: { path: { ...path, champ: c.nom } } })}
                    >
                      <Undo2Icon />
                    </Button>
                  </span>
                )}
              </div>
              {c.nom === "position" ? (
                <select
                  id={`${participant.playerId}-${c.nom}`}
                  className={CHAMP_NATIF}
                  value={valeurs.position}
                  onChange={(e) => setValeurs({ ...valeurs, position: e.target.value })}
                >
                  <option value="">-</option>
                  {VOIES.map((v) => (
                    <option key={v} value={v}>
                      {v}
                    </option>
                  ))}
                </select>
              ) : (
                <Input
                  id={`${participant.playerId}-${c.nom}`}
                  type={c.texte ? "text" : "number"}
                  min={c.texte ? undefined : 0}
                  value={valeurs[c.nom]}
                  onChange={(e) => setValeurs({ ...valeurs, [c.nom]: e.target.value })}
                />
              )}
            </div>
          );
        })}
      </div>
      <div>
        <Button size="sm" aria-label={`Enregistrer les stats de ${participant.prenom}`} disabled={modifies.length === 0 || corriger.isPending} onClick={enregistrer}>
          Enregistrer
        </Button>
      </div>
    </div>
  );
}

/** Les écarts d'un import refusé (409) : la liste des différences entre le fichier et la game. */
const ecartsDe = (erreur: unknown): string[] | null => {
  const ecarts = (erreur as { ecarts?: unknown } | null)?.ecarts;
  return Array.isArray(ecarts) ? ecarts.map(String) : null;
};

/**
 * L'Import d'un Rapport (ADR 0020) : le Fichier de Rapport remis par un joueur dont le Companion n'a pas
 * pu envoyer. Il doit correspondre à la game (participants, Sides, vainqueur), sinon il est refusé en
 * entier et les écarts s'affichent.
 */
function ImportDeRapport({ gameId, onErreur }: { gameId: number } & SurErreur) {
  const relire = useRelire();
  const [fichier, setFichier] = useState<File | null>(null);
  const [refus, setRefus] = useState<string | null>(null);
  const [ecarts, setEcarts] = useState<string[] | null>(null);
  const [importe, setImporte] = useState(false);
  const importer = $api.useMutation("post", "/games/{id}/import-rapport", {
    onError: (e) => {
      const liste = ecartsDe(e);
      if (liste) setEcarts(liste);
      else onErreur(e);
    },
    onSuccess: async () => {
      setImporte(true);
      await relire();
    },
  });

  const lancer = async () => {
    if (!fichier) return;
    onErreur(null);
    setEcarts(null);
    setImporte(false);
    let rapport: unknown;
    try {
      rapport = JSON.parse(await fichier.text());
    } catch {
      setRefus("Ce fichier n'est pas un fichier JSON : déposez le Fichier de Rapport tel que reçu.");
      return;
    }
    setRefus(null);
    importer.mutate({ params: { path: { id: gameId } }, body: rapport as Schemas["RapportDeGameRequest"] });
  };

  return (
    <section className="flex flex-col gap-3">
      <h3 className="font-semibold">Import d'un Rapport</h3>
      <p className="text-sm text-muted-foreground">
        Déposez le Fichier de Rapport d'un joueur dont le Companion n'a pas pu envoyer. Ses participants, ses Sides et son vainqueur doivent
        être ceux de la game : le vainqueur et la composition ne changent jamais, les stats, les champions et les costs si.
      </p>
      <div className="flex flex-wrap items-end gap-2">
        <div className="flex flex-col gap-1">
          <Label htmlFor={`rapport-${gameId}`} className="text-xs">
            Fichier de Rapport
          </Label>
          <Input
            id={`rapport-${gameId}`}
            type="file"
            accept=".json,application/json"
            onChange={(e) => setFichier(e.target.files?.[0] ?? null)}
          />
        </div>
        <Button size="sm" disabled={!fichier || importer.isPending} onClick={lancer}>
          Importer le rapport
        </Button>
      </div>
      {refus && <p className="text-sm text-destructive">{refus}</p>}
      {importe && <p className="text-sm text-muted-foreground">Rapport importé.</p>}
      {ecarts && (
        <div role="alert" className="flex flex-col gap-1 rounded-md border border-destructive/50 p-3 text-sm">
          <span className="font-medium text-destructive">Import refusé : le fichier ne correspond pas à la game, rien n'a été importé.</span>
          <ul className="list-disc pl-5">
            {ecarts.map((e) => (
              <li key={e}>{e}</li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}

function Suppression({ gameId, onErreur, onSupprimee }: { gameId: number; onSupprimee: () => void } & SurErreur) {
  const relire = useRelire();
  const [confirmer, setConfirmer] = useState(false);
  const supprimer = $api.useMutation("delete", "/games/{id}", {
    onMutate: () => onErreur(null),
    onError: (e) => {
      setConfirmer(false);
      onErreur(e);
    },
    onSuccess: async () => {
      onSupprimee();
      await relire();
    },
  });
  return (
    <section className="flex flex-col gap-2 border-t pt-4">
      <h3 className="font-semibold">Zone dangereuse</h3>
      <div>
        <Button variant="destructive" size="sm" onClick={() => setConfirmer(true)}>
          <Trash2Icon />
          Supprimer la game
        </Button>
      </div>
      <Dialog open={confirmer} onOpenChange={setConfirmer}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Supprimer la game #{gameId} ?</DialogTitle>
            <DialogDescription>
              La game disparaît pour tout le monde, définitivement. La série et le cost se recalculent sans elle ; une série vidée est
              effacée.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmer(false)}>
              Annuler
            </Button>
            <Button variant="destructive" disabled={supprimer.isPending} onClick={() => supprimer.mutate({ params: { path: { id: gameId } } })}>
              Supprimer définitivement
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </section>
  );
}
