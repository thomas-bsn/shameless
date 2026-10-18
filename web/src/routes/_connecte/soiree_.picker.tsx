import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { Check } from "lucide-react";
import { type ReactNode, useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { PageHeader } from "@/components/page-header";
import { reduireLesAnimations, TirageDialog } from "@/components/picker-tirage";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { SeriesOptionsForm } from "@/components/series-options-form";
import { TeamCard } from "@/components/team-card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { points } from "@/lib/cost";
import { ajouterCandidats, joueursLibres, PICKER_STEPS, type PickerStep, pickerStep } from "@/lib/picker";
import { matchesPlayer } from "@/lib/players";
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
  selection: "Coche les dix joueurs de la prochaine série.",
  methode: "Forme les deux Équipes par Draft ou par Roulette.",
  draft: "Les capitaines choisissent leurs joueurs à tour de rôle.",
  equipes: "Voilà les équipes : garde-les, relance le tirage ou change de méthode.",
  autorole: "Attribue les rôles (hors rôle ou aléatoire), ou passe cette étape.",
  serie: "Choisis le format et Fearless, puis crée la Série.",
};

/** Toute commande du Picker renvoie l'état à jour : on relit la Soirée ouverte qui le porte. */
function usePickerRefresh() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: ["get", "/soirees/open"] });
}

function PickerPage() {
  const soiree = $api.useQuery("get", "/soirees/open");

  if (soiree.isPending) return <LoadingRows />;
  if (soiree.isError) return <ErrorAlert error={soiree.error} />;
  if (!soiree.data.soiree) {
    return <SoireeFermee />;
  }
  return <PickerSession picker={soiree.data.soiree.picker} />;
}

/** La Soirée s'est fermée (toute seule, ou à la main) pendant le Picker : on l'explique. */
function SoireeFermee() {
  const refresh = usePickerRefresh();
  const ouvrir = $api.useMutation("post", "/soirees", { onSuccess: () => refresh() });

  return (
    <Card>
      <CardContent className="flex flex-col items-center gap-4 py-10 text-center">
        <h1 className="text-2xl font-bold">La soirée s'est fermée</h1>
        <p className="max-w-md text-muted-foreground">
          Une soirée se ferme toute seule après un moment sans game, et le Picker qui y était rattaché n'est plus.
          Ouvre une nouvelle soirée pour reformer les équipes.
        </p>
        {ouvrir.isError && <ErrorAlert error={ouvrir.error} />}
        <Button size="lg" disabled={ouvrir.isPending} onClick={() => ouvrir.mutate({})}>
          Ouvrir une nouvelle soirée
        </Button>
        <Button asChild variant="ghost">
          <Link to="/soiree">Retour à la Soirée</Link>
        </Button>
      </CardContent>
    </Card>
  );
}

function PickerSession({ picker }: { picker: Picker | null }) {
  const [editingSelection, setEditingSelection] = useState(false);
  const [changingMethode, setChangingMethode] = useState(false);
  // Autorole et Série ne se lisent pas dans l'état de l'API : l'écran s'en souvient.
  const [apresEquipes, setApresEquipes] = useState<"equipes" | "autorole" | "serie">("equipes");
  const [tirage, setTirage] = useState(false);
  const base = editingSelection ? "selection" : pickerStep(picker);
  const step: PickerStep = base === "equipes" ? apresEquipes : base;

  // Un nouveau tirage ou une nouvelle Draft repart des Équipes ; le mouvement s'arrête si demandé.
  const nouveauxTirage = () => {
    setApresEquipes("equipes");
    if (!reduireLesAnimations()) setTirage(true);
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Picker" description={stepDescriptions[step]} actions={<Steps current={step} />} />
      {tirage && picker && <TirageDialog picker={picker} onClose={() => setTirage(false)} />}
      {step === "selection" || !picker ? (
        <Selection
          initial={picker?.players.map((p) => p.playerId) ?? []}
          onDone={() => {
            setEditingSelection(false);
            setChangingMethode(false);
          }}
          onCancel={picker ? () => setEditingSelection(false) : undefined}
        />
      ) : (
        <>
          <SelectedPlayers picker={picker} onEdit={() => setEditingSelection(true)} />
          {(step === "methode" || changingMethode) && (
            <Methode
              picker={picker}
              onDraft={() => {
                setApresEquipes("equipes");
                setChangingMethode(false);
              }}
              onRoulette={() => {
                nouveauxTirage();
                setChangingMethode(false);
              }}
              onCancel={step === "methode" ? undefined : () => setChangingMethode(false)}
            />
          )}
          {step === "draft" && <DraftEnCours picker={picker} />}
          {step === "equipes" && (
            <EquipesFormees
              picker={picker}
              onRelancer={nouveauxTirage}
              onGarder={() => setApresEquipes("autorole")}
            />
          )}
          {step === "autorole" && (
            <div className="flex flex-col gap-4">
              <Teams picker={picker} />
              <Autorole picker={picker} onDone={() => setApresEquipes("serie")} />
            </div>
          )}
          {step === "serie" && (
            <div className="flex flex-col gap-4">
              <Teams picker={picker} />
              <Valider />
              <div>
                <Button variant="ghost" onClick={() => setApresEquipes("autorole")}>
                  Retour à l'Autorole
                </Button>
              </div>
            </div>
          )}
          {(step === "draft" || step === "equipes") && !changingMethode && (
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

function Steps({ current }: { current: PickerStep }) {
  const courante = PICKER_STEPS.findIndex(([step]) => step === current);
  return (
    <ol aria-label="Étapes du Picker" className="flex flex-wrap gap-3 text-sm">
      {PICKER_STEPS.map(([step, label], i) => {
        const faite = i < courante;
        const active = i === courante;
        return (
          <li
            key={step}
            aria-current={active ? "step" : undefined}
            className={cn("flex items-center gap-2", active ? "font-semibold" : "text-muted-foreground")}
          >
            <span
              className={cn(
                "flex size-6 items-center justify-center rounded-full border text-xs",
                active && "border-primary bg-primary text-primary-foreground",
                faite && "border-primary text-foreground",
              )}
            >
              {faite ? <Check aria-hidden className="size-3.5" /> : i + 1}
            </span>
            {label}
          </li>
        );
      })}
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
          <span className={cn("ml-1", selected.length === SELECTION_SIZE ? "text-emerald-400" : "text-muted-foreground")}>
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
          <ErrorAlert error={players.error} onRetry={() => players.refetch()} />
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
            {selected.length >= SELECTION_SIZE
              ? "Choisir la méthode"
              : `Choisir la méthode · encore ${pluriel(SELECTION_SIZE - selected.length, "joueur")}`}
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

function Methode({
  picker,
  onDraft,
  onRoulette,
  onCancel,
}: {
  picker: Picker;
  onDraft: () => void;
  onRoulette: () => void;
  onCancel?: () => void;
}) {
  return (
    <div className="flex flex-col gap-4">
      <div className="grid gap-4 md:grid-cols-2">
        <LancerDraft picker={picker} onDone={onDraft} />
        <TirerRoulette onDone={onRoulette} />
      </div>
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
  const refresh = usePickerRefresh();
  const draft = $api.useMutation("post", "/soirees/open/picker/draft", {
    onSuccess: async () => {
      await refresh();
      onDone();
    },
  });

  const manuels = designation === "Manuels";
  const ready = !manuels || (capitaineA !== "" && capitaineB !== "");

  return (
    <Card>
      <CardHeader>
        <CardTitle>Draft</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
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
            Lancer la Draft
          </Button>
        </div>
      </CardContent>
    </Card>
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
  const refresh = usePickerRefresh();
  const roulette = $api.useMutation("post", "/soirees/open/picker/roulette", {
    onSuccess: async () => {
      await refresh();
      onDone();
    },
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Roulette</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <p className="text-sm text-muted-foreground">
          Aléatoire : deux équipes au pur hasard. Équilibrée : au hasard parmi les tirages dont l'écart de cost est
          d'au plus 1 point.
        </p>
        {roulette.isError && <ErrorAlert error={roulette.error} />}
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
      </CardContent>
    </Card>
  );
}

// --- Draft en cours ------------------------------------------------------------------------------

function DraftEnCours({ picker }: { picker: Picker }) {
  const refresh = usePickerRefresh();
  const pick = $api.useMutation("post", "/soirees/open/picker/draft/picks", { onSuccess: () => refresh() });
  const auTrait = picker.equipeAuTrait as Equipe;
  const capitaine = picker.players.find((p) => p.playerId === (auTrait === "A" ? picker.capitaineA : picker.capitaineB));

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
          {pick.isError && <ErrorAlert error={pick.error} />}
          <ul aria-label="Joueurs libres" className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
            {joueursLibres(picker).map((p) => (
              <li key={p.playerId}>
                <Button
                  variant="outline"
                  className="h-auto w-full justify-between py-2"
                  disabled={pick.isPending}
                  onClick={() => pick.mutate({ body: { equipe: auTrait, playerId: p.playerId } })}
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
    </div>
  );
}

// --- Équipes formées : Autorole et validation ---------------------------------------------------

function EquipesFormees({
  picker,
  onRelancer,
  onGarder,
}: {
  picker: Picker;
  onRelancer: () => void;
  onGarder: () => void;
}) {
  return (
    <div className="flex flex-col gap-4">
      <h2 className="text-xl font-semibold">Voilà les équipes</h2>
      <Teams picker={picker} />
      {picker.methode === "Roulette" && picker.variante ? (
        <RelancerRoulette variante={picker.variante} onRelancer={onRelancer} onGarder={onGarder} />
      ) : (
        <div>
          <Button onClick={onGarder}>Garder ces équipes</Button>
        </div>
      )}
    </div>
  );
}

function RelancerRoulette({
  variante,
  onRelancer,
  onGarder,
}: {
  variante: NonNullable<Schemas["RouletteVariante"]>;
  onRelancer: () => void;
  onGarder: () => void;
}) {
  const refresh = usePickerRefresh();
  const roulette = $api.useMutation("post", "/soirees/open/picker/roulette", {
    onSuccess: async () => {
      await refresh();
      onRelancer();
    },
  });
  const other = variante === "Equilibree" ? "Aleatoire" : "Equilibree";

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm text-muted-foreground">
          Roulette {variante === "Equilibree" ? "équilibrée" : "aléatoire"}
        </span>
        <Button variant="outline" disabled={roulette.isPending} onClick={() => roulette.mutate({ body: { variante } })}>
          Relancer le tirage
        </Button>
        <Button
          variant="ghost"
          disabled={roulette.isPending}
          onClick={() => roulette.mutate({ body: { variante: other } })}
        >
          Tirer en {other === "Equilibree" ? "Équilibrée" : "Aléatoire"}
        </Button>
        <Button onClick={onGarder}>Garder ces équipes</Button>
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
        <div className="flex flex-wrap gap-2">
          <Button disabled={autorole.isPending} onClick={() => autorole.mutate({ body: { variante } })}>
            {done ? "Relancer l'Autorole" : "Lancer l'Autorole"}
          </Button>
          <Button variant="ghost" onClick={onDone}>
            Passer
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
          submitLabel="Créer la série"
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
        Écart de cost : {points(picker.ecartCost)}
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
