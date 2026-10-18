import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { type ReactNode, useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { PageHeader } from "@/components/page-header";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { SeriesOptionsForm } from "@/components/series-options-form";
import { TeamCard } from "@/components/team-card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogDescription, DialogTitle } from "@/components/ui/dialog";
import { points } from "@/lib/cost";
import { ajouterCandidats, joueursLibres, type PickerStep, pickerStep } from "@/lib/picker";
import { matchesPlayer } from "@/lib/players";
import { problemMessage } from "@/lib/problem";
import { cn } from "@/lib/utils";

// « soiree_ » : la page n'est pas imbriquée dans le layout de l'écran Soirée.
export const Route = createFileRoute("/_connecte/soiree_/picker")({
  component: PickerPage,
});

type Picker = Schemas["PickerResponse"];
type Participant = Schemas["GameParticipantDto"];
type Equipe = "A" | "B";

const SELECTION_SIZE = 10;

const stepDescriptions: Record<PickerStep, string> = {
  selection: "Choisis les dix joueurs de la prochaine Série.",
  methode: "Choisis la Draft ou la Roulette pour former les Équipes.",
  draft: "Les capitaines choisissent leurs joueurs à tour de rôle.",
  equipes: "Vérifie les Équipes et leur écart de cost.",
  autorole: "Attribue un rôle à chaque joueur ou passe cette étape.",
  serie: "Choisis le format et Fearless pour créer la Série.",
};

/** Toute commande du Picker renvoie l'état à jour : on relit la Soirée ouverte qui le porte. */
function usePickerRefresh() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: ["get", "/soirees/open"] });
}

function PickerPage() {
  const queryClient = useQueryClient();
  const soiree = $api.useQuery("get", "/soirees/open");
  const ouvrir = $api.useMutation("post", "/soirees", {
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["get", "/soirees/open"] });
    },
  });

  if (soiree.isPending) return <LoadingRows />;
  if (soiree.isError) return <ErrorAlert error={soiree.error} />;
  if (!soiree.data.soiree) {
    if (soiree.data.pickerInterrompu) {
      return <Card><CardHeader><h1 className="text-xl font-semibold">La Soirée a été fermée</h1></CardHeader><CardContent className="flex flex-col items-start gap-3"><p className="text-muted-foreground">La Soirée en cours a été fermée automatiquement pendant le Picker. Ouvre-en une nouvelle pour reprendre.</p>{ouvrir.isError && <ErrorAlert error={ouvrir.error} />}<Button disabled={ouvrir.isPending} onClick={() => ouvrir.mutate({})}>Ouvrir une nouvelle Soirée</Button></CardContent></Card>;
    }
    return <><PageHeader title="Picker" /><Card><CardContent className="flex flex-col items-start gap-3"><p className="text-muted-foreground">Aucune Soirée n'est ouverte : ouvre-en une pour lancer le Picker.</p><Button asChild variant="outline"><Link to="/soiree">Aller à la Soirée</Link></Button></CardContent></Card></>;
  }
  return <PickerSession picker={soiree.data.soiree.picker} />;
}

function PickerSession({ picker }: { picker: Picker | null }) {
  const [editingSelection, setEditingSelection] = useState(false);
  const [changingMethode, setChangingMethode] = useState(false);
  const [finalStep, setFinalStep] = useState<"equipes" | "autorole" | "serie">(() => {
    if (picker?.canValidate && [...picker.teamA, ...picker.teamB].some((p) => p.role)) return "serie";
    return "equipes";
  });
  const inferredStep = editingSelection ? "selection" : pickerStep(picker);
  const step = inferredStep === "equipes" ? finalStep : inferredStep;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Picker" description={stepDescriptions[step]} actions={<Steps current={step} />} />
      {step === "selection" || !picker ? (
        <Selection
          initial={picker?.players.map((p) => p.playerId) ?? []}
          onDone={() => {
            setEditingSelection(false);
            setChangingMethode(false);
            setFinalStep("equipes");
          }}
          onCancel={picker ? () => setEditingSelection(false) : undefined}
        />
      ) : (
        <>
          <SelectedPlayers picker={picker} onEdit={() => setEditingSelection(true)} />
          {(step === "methode" || changingMethode) && (
            <Methode
              picker={picker}
              onDone={() => {
                setChangingMethode(false);
                setFinalStep("equipes");
              }}
              onCancel={step === "methode" ? undefined : () => setChangingMethode(false)}
            />
          )}
          {step === "draft" && <DraftEnCours picker={picker} />}
          {step === "equipes" && <EquipesFormees picker={picker} onContinue={() => setFinalStep("autorole")} />}
          {step === "autorole" && (
            <AutoroleEtape
              picker={picker}
              onDone={() => setFinalStep("serie")}
              onSkip={() => setFinalStep("serie")}
            />
          )}
          {step === "serie" && <SerieEtape picker={picker} onEditAutorole={() => setFinalStep("autorole")} />}
          {step !== "methode" && !changingMethode && (
            <div>
              <Button variant="outline" onClick={() => setChangingMethode(true)}>
                {picker.methode === "Draft" ? "Relancer la Draft ou passer à la Roulette" : "Passer à la Draft"}
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}

const stepLabels: [PickerStep, string][] = [
  ["selection", "Joueurs"],
  ["methode", "Méthode"],
  ["draft", "Draft"],
  ["equipes", "Équipes"],
  ["autorole", "Autorole"],
  ["serie", "Série"],
];

function Steps({ current }: { current: PickerStep }) {
  return (
    <ol aria-label="Étapes du Picker" className="flex flex-wrap gap-1 text-xs">
      {stepLabels.map(([step, label]) => (
        <li key={step}>
          <Badge variant={step === current ? "default" : "outline"} aria-current={step === current ? "step" : undefined}>
            {label}
          </Badge>
        </li>
      ))}
    </ol>
  );
}

// --- Sélection des dix -----------------------------------------------------------------------

function Selection({ initial, onDone, onCancel }: { initial: number[]; onDone: () => void; onCancel?: () => void }) {
  const players = $api.useQuery("get", "/players");
  const [selected, setSelected] = useState<number[]>(initial);
  const [search, setSearch] = useState("");
  const refresh = usePickerRefresh();
  const select = $api.useMutation("put", "/soirees/open/picker/selection", {
    onSuccess: async () => {
      await refresh();
      onDone();
    },
  });

  // Le navigateur demande à l'API, qui interroge le bot Discord avec sa propre clé.
  const vocal = $api.useMutation("get", "/soirees/open/picker/candidats-vocaux", {
    onSuccess: (candidats) =>
      setSelected((current) => ajouterCandidats(current, candidats.joueurs.map((j) => j.playerId), SELECTION_SIZE)),
  });

  const toggle = (id: number) =>
    setSelected((current) => (current.includes(id) ? current.filter((x) => x !== id) : [...current, id]));

  const shown = (players.data ?? [])
    .filter((p) => matchesPlayer(p, search))
    .sort((a, b) => a.prenom.localeCompare(b.prenom, "fr"));

  return (
    <Card>
      <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3">
        <CardTitle>
          Joueurs cochés{" "}
          <span className={cn("ml-1", selected.length === SELECTION_SIZE ? "text-emerald-600 dark:text-emerald-400" : "text-muted-foreground")}>
            {selected.length}/{SELECTION_SIZE}
          </span>
        </CardTitle>
        <Input
          aria-label="Rechercher un joueur"
          placeholder="Rechercher un joueur"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="max-w-xs"
        />
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {players.isPending ? (
          <LoadingRows />
        ) : players.isError ? (
          <Alert variant="destructive">
            <AlertTitle>Impossible de charger les joueurs</AlertTitle>
            <AlertDescription className="flex flex-wrap items-center justify-between gap-2">
              <span>{problemMessage(players.error)}</span>
              <Button variant="outline" onClick={() => void players.refetch()}>
                Réessayer
              </Button>
            </AlertDescription>
          </Alert>
        ) : (
          <ul className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {shown.map((p) => {
              const checked = selected.includes(p.id);
              return (
                <li key={p.id}>
                  <label
                    className={cn(
                      "flex cursor-pointer items-center gap-3 rounded-md border px-3 py-2 text-sm hover:bg-accent",
                      checked && "border-primary bg-accent/50",
                    )}
                  >
                    <Checkbox checked={checked} onCheckedChange={() => toggle(p.id)} />
                    <span className="flex-1 truncate">
                      {p.prenom} <span className="text-xs text-muted-foreground">{p.riotId}</span>
                    </span>
                    <span className="text-xs text-muted-foreground">{points(p.currentCost)}</span>
                  </label>
                </li>
              );
            })}
          </ul>
        )}
        {vocal.isError && <ErrorAlert error={vocal.error} />}
        {vocal.data && <CandidatsVocauxAjoutes candidats={vocal.data} />}
        {select.isError && <ErrorAlert error={select.error} />}
        <div className="flex flex-wrap gap-2">
          <Button
            disabled={selected.length !== SELECTION_SIZE || select.isPending}
            onClick={() => select.mutate({ body: { playerIds: selected } })}
          >
            Valider la sélection
          </Button>
          <Button variant="outline" disabled={vocal.isPending} onClick={() => vocal.mutate({})}>
            Ajouter les joueurs en vocal
          </Button>
          {selected.length > 0 && (
            <Button variant="ghost" onClick={() => setSelected([])}>
              Tout décocher
            </Button>
          )}
          {onCancel && (
            <Button variant="ghost" onClick={onCancel}>
              Annuler
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

const pluriel = (n: number, mot: string) => `${n} ${mot}${n > 1 ? "s" : ""}`;

function CandidatsVocauxAjoutes({ candidats }: { candidats: Schemas["CandidatsVocaux"] }) {
  if (!candidats.channelId) return <p className="text-sm text-muted-foreground">Personne n'est en vocal.</p>;
  return (
    <p className="text-sm text-muted-foreground">
      {pluriel(candidats.joueurs.length, "joueur")} en vocal ajouté{candidats.joueurs.length > 1 ? "s" : ""}
      {candidats.membresNonLies > 0 && ` · ${pluriel(candidats.membresNonLies, "membre")} en vocal sans joueur lié`}
    </p>
  );
}

function SelectedPlayers({ picker, onEdit }: { picker: Picker; onEdit: () => void }) {
  return (
    <section aria-label="Les dix joueurs" className="flex flex-wrap items-center gap-2">
      {picker.players.map((p) => (
        <Badge key={p.playerId} variant="secondary">
          {p.prenom}
        </Badge>
      ))}
      <Button variant="link" size="sm" onClick={onEdit}>
        Changer la sélection
      </Button>
    </section>
  );
}

// --- Méthode : Draft ou Roulette ---------------------------------------------------------------

function Methode({ picker, onDone, onCancel }: { picker: Picker; onDone: () => void; onCancel?: () => void }) {
  const [draftOpen, setDraftOpen] = useState(false);
  const [rouletteOpen, setRouletteOpen] = useState(false);
  const refresh = usePickerRefresh();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap gap-3">
        <Button onClick={() => setDraftOpen(true)}>Choisir la Draft</Button>
        <Button variant="secondary" onClick={() => setRouletteOpen(true)}>Choisir la Roulette</Button>
      </div>
      <Dialog open={draftOpen} onOpenChange={setDraftOpen}>
        <DialogContent aria-describedby="draft-description">
          <DialogTitle>Configurer la Draft</DialogTitle>
          <DialogDescription id="draft-description">Désigne les capitaines et choisis qui sélectionne en premier.</DialogDescription>
          <LancerDraft picker={picker} onDone={async () => { await refresh(); setDraftOpen(false); onDone(); }} />
        </DialogContent>
      </Dialog>
      <Dialog open={rouletteOpen} onOpenChange={setRouletteOpen}>
        <DialogContent aria-describedby="roulette-description">
          <DialogTitle>Tirage de la Roulette</DialogTitle>
          <DialogDescription id="roulette-description">Tire les deux Équipes au hasard ou cherche un tirage équilibré.</DialogDescription>
          <TirerRoulette onDone={async () => { await refresh(); setRouletteOpen(false); onDone(); }} />
        </DialogContent>
      </Dialog>
      {onCancel && (
        <div>
          <Button variant="ghost" onClick={onCancel}>
            Garder les Équipes actuelles
          </Button>
        </div>
      )}
    </div>
  );
}

const designations: [Schemas["DesignationCapitaines"], string][] = [
  ["PlusGrosCosts", "Les deux plus gros costs"],
  ["AuHasard", "Au hasard"],
  ["Manuels", "À la main"],
];

const PILE_OU_FACE = "pile-ou-face";

function LancerDraft({ picker, onDone }: { picker: Picker; onDone: () => void }) {
  const [designation, setDesignation] = useState<Schemas["DesignationCapitaines"]>("PlusGrosCosts");
  const [capitaineA, setCapitaineA] = useState<string>("");
  const [capitaineB, setCapitaineB] = useState<string>("");
  const [premierPick, setPremierPick] = useState<string>(PILE_OU_FACE);
  const [result, setResult] = useState<Picker | null>(null);
  const draft = $api.useMutation("post", "/soirees/open/picker/draft", { onSuccess: setResult });

  const manuels = designation === "Manuels";
  const ready = !manuels || (capitaineA !== "" && capitaineB !== "");

  if (result) {
    return (
      <div className="flex flex-col gap-4">
        <p className="font-medium">{result.premierPick ? `L'Équipe ${result.premierPick} choisit en premier.` : "La Draft est prête."}</p>
        {result.premierPick && <p aria-label="Résultat du pile ou face" className="rounded-lg border p-4 text-center text-2xl font-semibold">Équipe {result.premierPick} choisit en premier</p>}
        <Button onClick={onDone}>Continuer la Draft</Button>
      </div>
    );
  }

  return (
      <div className="flex flex-col gap-4">
        <Field label="Capitaines">
          <Select value={designation} onValueChange={(v) => setDesignation(v as Schemas["DesignationCapitaines"])}>
            <SelectTrigger aria-label="Capitaines" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {designations.map(([value, label]) => (
                <SelectItem key={value} value={value}>
                  {label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>
        {manuels && (
          <div className="grid grid-cols-2 gap-3">
            <CapitaineSelect label="Capitaine A" players={picker.players} value={capitaineA} onChange={setCapitaineA} />
            <CapitaineSelect label="Capitaine B" players={picker.players} value={capitaineB} onChange={setCapitaineB} />
          </div>
        )}
        <Field label="Premier pick">
          <Select value={premierPick} onValueChange={setPremierPick}>
            <SelectTrigger aria-label="Premier pick" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={PILE_OU_FACE}>Pile ou face</SelectItem>
              <SelectItem value="A">Équipe A</SelectItem>
              <SelectItem value="B">Équipe B</SelectItem>
            </SelectContent>
          </Select>
        </Field>
        {draft.isError && <ErrorAlert error={draft.error} />}
        <div>
          <Button
            disabled={!ready || draft.isPending}
            onClick={() =>
              draft.mutate({
                body: {
                  capitaines: designation,
                  capitaineA: manuels ? Number(capitaineA) : null,
                  capitaineB: manuels ? Number(capitaineB) : null,
                  premierPick: premierPick === PILE_OU_FACE ? null : premierPick,
                },
              })
            }
          >
            {premierPick === PILE_OU_FACE ? "Lancer le pile ou face" : "Lancer la Draft"}
          </Button>
        </div>
      </div>
  );
}

function CapitaineSelect({
  label,
  players,
  value,
  onChange,
}: {
  label: string;
  players: Participant[];
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <Field label={label}>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger aria-label={label} className="w-full">
          <SelectValue placeholder="Choisir" />
        </SelectTrigger>
        <SelectContent>
          {players.map((p) => (
            <SelectItem key={p.playerId} value={String(p.playerId)}>
              {p.prenom}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </Field>
  );
}

function TirerRoulette({ onDone }: { onDone: () => void }) {
  const [result, setResult] = useState<Picker | null>(null);
  const roulette = $api.useMutation("post", "/soirees/open/picker/roulette", { onSuccess: setResult });

  return (
      <div className="flex flex-col gap-4">
        <p className="text-sm text-muted-foreground">
          Aléatoire : deux équipes au pur hasard. Équilibrée : au hasard parmi les tirages dont l'écart de cost est
          d'au plus 1 point.
        </p>
        {roulette.isPending && <p aria-live="polite" className="flex items-center gap-2 text-sm"><span aria-hidden="true" className="inline-block size-4 animate-spin rounded-full border-2 border-current border-r-transparent motion-reduce:animate-none" />Tirage en cours…</p>}
        {roulette.isError && <ErrorAlert error={roulette.error} />}
        {result && <><Teams picker={result} /><p className="text-sm text-muted-foreground">Cost A : {points(result.teamACost)} · Cost B : {points(result.teamBCost)} · Écart : {points(result.ecartCost)}</p><Button onClick={onDone}>Continuer avec ces Équipes</Button></>}
        <div className="flex flex-wrap gap-2">
          <Button
            disabled={roulette.isPending}
            onClick={() => roulette.mutate({ body: { variante: "Equilibree" } })}
          >
            Roulette Équilibrée
          </Button>
          <Button
            variant="secondary"
            disabled={roulette.isPending}
            onClick={() => roulette.mutate({ body: { variante: "Aleatoire" } })}
          >
            Roulette Aléatoire
          </Button>
        </div>
      </div>
  );
}

// --- Draft en cours ------------------------------------------------------------------------------

function DraftEnCours({ picker }: { picker: Picker }) {
  const refresh = usePickerRefresh();
  const [selected, setSelected] = useState<Participant | null>(null);
  const pick = $api.useMutation("post", "/soirees/open/picker/draft/picks", {
    onSuccess: async () => {
      await refresh();
      setSelected(null);
    },
  });
  const auTrait = picker.equipeAuTrait as Equipe;
  const capitaine = picker.players.find((p) => p.playerId === (auTrait === "A" ? picker.capitaineA : picker.capitaineB));
  const teamCost = auTrait === "A" ? picker.teamACost : picker.teamBCost;

  return (
    <div className="flex flex-col gap-4">
      <Teams picker={picker} highlight={auTrait} />
      <Card>
        <CardHeader>
          <CardTitle>
            Au tour de l'Équipe {auTrait}
            {capitaine && <span className="ml-2 font-normal text-muted-foreground">({capitaine.prenom} choisit)</span>}
          </CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <ul aria-label="Joueurs libres" className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
            {joueursLibres(picker).map((p) => (
              <li key={p.playerId}>
                <Button
                  variant="outline"
                  className="h-auto w-full justify-between py-2"
                  onClick={() => setSelected(p)}
                  aria-label={`Prendre ${p.prenom} dans l'Équipe ${auTrait}`}
                >
                  <span className="truncate">{p.prenom}</span>
                  <span className="text-xs text-muted-foreground">{points(p.cost ?? 0)}</span>
                </Button>
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>
      <Dialog open={selected !== null} onOpenChange={(open) => { if (!open && !pick.isPending) setSelected(null); }}>
        <DialogContent aria-describedby="pick-description">
          <DialogTitle>{selected ? `Confirmer le choix de ${selected.prenom}` : "Confirmer le choix"}</DialogTitle>
          <DialogDescription id="pick-description">
            L'Équipe {auTrait} choisit ce joueur. Vérifie le cost avant de confirmer.
          </DialogDescription>
          {selected && (
            <div className="grid grid-cols-2 gap-3 rounded-lg border p-4 text-sm">
              <p>Cost de l'Équipe {auTrait} avant : <strong>{points(teamCost)}</strong></p>
              <p>Cost de l'Équipe {auTrait} après : <strong>{points(teamCost + (selected.cost ?? 0))}</strong></p>
            </div>
          )}
          {pick.isError && <ErrorAlert error={pick.error} />}
          <div className="flex justify-end gap-2">
            <Button variant="outline" disabled={pick.isPending} onClick={() => setSelected(null)}>Annuler</Button>
            <Button
              disabled={!selected || pick.isPending}
              onClick={() => selected && pick.mutate({ body: { equipe: auTrait, playerId: selected.playerId } })}
            >
              Confirmer le choix
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}

// --- Équipes formées : Autorole et validation ---------------------------------------------------

function EquipesFormees({ picker, onContinue }: { picker: Picker; onContinue: () => void }) {
  return (
    <div className="flex flex-col gap-4">
      <Teams picker={picker} />
      {picker.methode === "Roulette" && picker.variante && <RelancerRoulette variante={picker.variante} />}
      <Button className="self-start" onClick={onContinue}>Continuer vers l'Autorole</Button>
    </div>
  );
}

function AutoroleEtape({ picker, onDone, onSkip }: { picker: Picker; onDone: () => void; onSkip: () => void }) {
  return (
    <div className="flex flex-col gap-4">
      <Teams picker={picker} />
      <Autorole picker={picker} onDone={onDone} />
      <Button variant="outline" className="self-start" onClick={onSkip}>Passer l'Autorole</Button>
    </div>
  );
}

function SerieEtape({ picker, onEditAutorole }: { picker: Picker; onEditAutorole: () => void }) {
  return (
    <div className="flex flex-col gap-4">
      <Teams picker={picker} />
      <Button variant="outline" className="self-start" onClick={onEditAutorole}>Revenir à l'Autorole</Button>
      <Valider />
    </div>
  );
}

function RelancerRoulette({ variante }: { variante: NonNullable<Schemas["RouletteVariante"]> }) {
  const refresh = usePickerRefresh();
  const roulette = $api.useMutation("post", "/soirees/open/picker/roulette", { onSuccess: () => refresh() });
  const other = variante === "Equilibree" ? "Aleatoire" : "Equilibree";

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm text-muted-foreground">
          Roulette {variante === "Equilibree" ? "Équilibrée" : "Aléatoire"}
        </span>
        <Button disabled={roulette.isPending} onClick={() => roulette.mutate({ body: { variante } })}>
          Relancer
        </Button>
        <Button
          variant="ghost"
          disabled={roulette.isPending}
          onClick={() => roulette.mutate({ body: { variante: other } })}
        >
          Tirer en {other === "Equilibree" ? "Équilibrée" : "Aléatoire"}
        </Button>
      </div>
      {roulette.isError && <ErrorAlert error={roulette.error} />}
    </div>
  );
}

function Autorole({ picker, onDone }: { picker: Picker; onDone: () => void }) {
  const [variante, setVariante] = useState<Schemas["AutoroleVariante"]>("HorsRole");
  const refresh = usePickerRefresh();
  const autorole = $api.useMutation("post", "/soirees/open/picker/autorole", {
    onSuccess: async () => {
      await refresh();
      onDone();
    },
  });
  const done = [...picker.teamA, ...picker.teamB].some((p) => p.role);

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          Autorole <span className="text-sm font-normal text-muted-foreground">facultatif</span>
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <Field label="Variante">
          <Select value={variante} onValueChange={(v) => setVariante(v as Schemas["AutoroleVariante"])}>
            <SelectTrigger aria-label="Variante d'Autorole" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="HorsRole">Hors rôle (personne sur son rôle principal)</SelectItem>
              <SelectItem value="Aleatoire">Aléatoire</SelectItem>
            </SelectContent>
          </Select>
        </Field>
        {autorole.isError && <ErrorAlert error={autorole.error} />}
        <div>
          <Button
            variant="secondary"
            disabled={autorole.isPending}
            onClick={() => autorole.mutate({ body: { variante } })}
          >
            {done ? "Relancer l'Autorole" : "Lancer l'Autorole"}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

function Valider() {
  const refresh = usePickerRefresh();
  const navigate = useNavigate();
  const validate = $api.useMutation("post", "/soirees/open/picker/validate", {
    onSuccess: async ({ seriesId }) => {
      await refresh();
      await navigate({ href: `/series/${seriesId}` });
    },
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Créer la Série</CardTitle>
      </CardHeader>
      <CardContent>
        <SeriesOptionsForm
          submitLabel="Valider et créer la Série"
          pending={validate.isPending}
          error={validate.isError ? validate.error : null}
          onSubmit={(options) => validate.mutate({ body: options })}
        />
      </CardContent>
    </Card>
  );
}

// --- Composants communs --------------------------------------------------------------------------

function Teams({ picker, highlight }: { picker: Picker; highlight?: Equipe }) {
  return (
    <section aria-label="Équipes" className="flex flex-col gap-2">
      <div className="grid gap-4 md:grid-cols-2">
        <TeamCard
          equipe="A"
          players={picker.teamA}
          cost={picker.teamACost}
          capitaine={picker.capitaineA}
          highlighted={highlight === "A"}
        />
        <TeamCard
          equipe="B"
          players={picker.teamB}
          cost={picker.teamBCost}
          capitaine={picker.capitaineB}
          highlighted={highlight === "B"}
        />
      </div>
      <p className="text-sm text-muted-foreground">
        Écart de cost : <span className="font-semibold text-foreground">{points(picker.ecartCost)}</span>
      </p>
    </section>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-2">
      <Label>{label}</Label>
      {children}
    </div>
  );
}
