import { useQueryClient } from "@tanstack/react-query";
import { Ban, Timer } from "lucide-react";
import { useEffect, useMemo, useState, type ReactNode } from "react";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { TeamCard } from "@/components/team-card";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  championsPris,
  filtrerChampions,
  libelleTour,
  peutJouer,
  secondesRestantes,
  toursDeLaDraft,
  type TourDeLaDraft,
} from "@/lib/draft-champions";
import { POLLING_DRAFT_MS, useChampions } from "@/lib/draft-champions-queries";
import { problemStatus } from "@/lib/problem";
import { sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";

type Draft = Schemas["DraftChampionsResponse"];
type Champion = Schemas["ChampionResponse"];
type Portee = Schemas["PorteeLien"];

/** Revalidation du lien : un lien renouvelé est refusé dès le prochain appel. */
const POLLING_LIEN_MS = 5000;


/**
 * La Draft de champions ouverte par un lien d'accès (ADR 0010), sans connexion. Un Lien capitaine
 * permet de bannir et de choisir les champions de son Équipe quand c'est son tour ; en lecture seule
 * sinon. Suit la dernière Draft de la Série : la game suivante remplace la précédente dès son ouverture.
 */
export function DraftParLien({ lien, portee }: { lien: string; portee: Portee }) {
  const path = { params: { path: { lien } } };
  const acces = $api.useQuery("get", "/liens/{lien}", path, { refetchInterval: POLLING_LIEN_MS, retry: false });
  const draft = $api.useQuery("get", "/liens/{lien}/draft-champions", path, {
    refetchInterval: POLLING_DRAFT_MS,
    retry: false,
    enabled: acces.isSuccess,
  });
  const champions = useChampions();
  const queryClient = useQueryClient();
  const jouer = $api.useMutation("post", "/liens/{lien}/draft-champions/actions", {
    onSuccess: (data) => queryClient.setQueryData(["get", "/liens/{lien}/draft-champions", path], data),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["get", "/liens/{lien}/draft-champions"] }),
  });

  const titre = portee === "Capitaine" ? "Draft de champions — capitaine" : "Draft de champions — spectateur";

  if (acces.isError) {
    return (
      <Page titre={titre}>
        {problemStatus(acces.error) === 404 ? <LienInvalide /> : <ErrorAlert error={acces.error} />}
      </Page>
    );
  }
  if (acces.isPending) {
    return (
      <Page titre={titre}>
        <LoadingRows />
      </Page>
    );
  }

  // Un Lien spectateur sur la page capitaine ne donne aucune action.
  const equipe = portee === "Capitaine" && acces.data.portee === "Capitaine" ? acces.data.equipe : null;

  return (
    <Page titre={titre} sousTitre={equipe ? `Capitaine de l'Équipe ${equipe}` : "Lecture seule"}>
      {draft.isPending ? (
        <LoadingRows />
      ) : draft.isError && !draft.data ? (
        problemStatus(draft.error) === 404 ? (
          <p className="text-muted-foreground">
            La Draft de champions n'est pas encore ouverte : la page s'affichera dès que l'organisateur l'ouvrira.
          </p>
        ) : (
          <ErrorAlert error={draft.error} />
        )
      ) : (
        <DraftChampionsView
          draft={draft.data!}
          champions={champions.data ?? []}
          equipe={equipe}
          jeu={
            equipe && peutJouer(draft.data!, equipe)
              ? {
                  pending: jouer.isPending,
                  error: jouer.isError ? jouer.error : null,
                  onJouer: (tour, championId) => jouer.mutate({ ...path, body: { tour, championId } }),
                }
              : null
          }
        />
      )}
      {champions.isError && <ErrorAlert error={champions.error} />}
    </Page>
  );
}

function Page({ titre, sousTitre, children }: { titre: string; sousTitre?: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold">{titre}</h1>
        {sousTitre && <p className="mt-1 text-sm text-muted-foreground">{sousTitre}</p>}
      </div>
      {children}
    </div>
  );
}

function LienInvalide() {
  return (
    <Alert variant="destructive">
      <AlertTitle>Ce lien n'est pas valide</AlertTitle>
      <AlertDescription>
        Il est inconnu, a été renouvelé par l'organisateur, ou le Lien spectateur n'est pas encore disponible.
        Demande le lien à jour à l'organisateur.
      </AlertDescription>
    </Alert>
  );
}

/** Jouer le tour courant : le capitaine par son lien, ou l'organisateur à sa place une fois l'échéance atteinte. */
export interface JeuDuTour {
  /** Titre du choix ; par défaut « Bannir un champion » ou « Choisir un champion ». */
  titre?: string;
  pending: boolean;
  error: unknown;
  onJouer: (tour: number, championId: string) => void;
}

/** L'instant courant, rafraîchi régulièrement : temps restant et échéance du tour. */
export function useMaintenant(intervalleMs = 250): number {
  const [maintenant, setMaintenant] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setMaintenant(Date.now()), intervalleMs);
    return () => clearInterval(id);
  }, [intervalleMs]);
  return maintenant;
}

/**
 * La Draft de champions d'une game : tour courant et temps restant, bans et picks de chaque Side,
 * ordre Tournament. Avec un `jeu`, le choix du champion du tour courant.
 */
export function DraftChampionsView({
  draft,
  champions,
  equipe,
  jeu,
  consigne,
  avecEquipes = true,
}: {
  draft: Draft;
  champions: Champion[];
  /** L'Équipe du Lien capitaine ; null pour le spectateur et l'organisateur. */
  equipe: string | null;
  /** Le tour courant peut être joué ici ; null en lecture seule. */
  jeu: JeuDuTour | null;
  /** Remplace la consigne du tour courant (« Au tour de l'Équipe A »). */
  consigne?: string;
  /** Affiche les Équipes et leurs rôles sous la Draft. */
  avecEquipes?: boolean;
}) {
  const parId = useMemo(() => new Map(champions.map((c) => [c.id, c])), [champions]);
  const tours = toursDeLaDraft(draft);
  const blue = draft.equipeBlue;
  const red = draft.equipeRed;

  return (
    <div className="flex flex-col gap-6">
      <EtatDuTour draft={draft} equipe={equipe} consigne={consigne} />

      <div className="grid gap-4 md:grid-cols-2">
        <CoteDraft side="blue" equipe={blue} tours={tours} parId={parId} />
        <CoteDraft side="red" equipe={red} tours={tours} parId={parId} />
      </div>

      {jeu && draft.tourCourant && (
        <ChoixChampion key={draft.tourCourant.numero} draft={draft} champions={champions} jeu={jeu} />
      )}

      <OrdreTournament tours={tours} parId={parId} />

      {avecEquipes && (
        <>
          <div className="grid gap-4 md:grid-cols-2">
            <TeamCard equipe="A" players={draft.teamA} capitaine={draft.capitaineA} highlighted={equipe === "A"} />
            <TeamCard equipe="B" players={draft.teamB} capitaine={draft.capitaineB} highlighted={equipe === "B"} />
          </div>
          {!draft.terminee && (
            <p className="text-sm text-muted-foreground">Les rôles de l'Autorole s'afficheront à la fin de la Draft.</p>
          )}
        </>
      )}
    </div>
  );
}

function EtatDuTour({ draft, equipe, consigne }: { draft: Draft; equipe: string | null; consigne?: string }) {
  const tour = draft.tourCourant;
  if (draft.terminee || !tour) {
    return (
      <Alert>
        <AlertTitle>Draft terminée</AlertTitle>
        <AlertDescription>Les champions et les rôles de la game sont fixés.</AlertDescription>
      </Alert>
    );
  }

  const aToi = equipe !== null && tour.equipe === equipe;
  return (
    <Card className={cn(aToi && "border-primary")}>
      <CardContent className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <p aria-label="Tour courant" className="text-lg font-semibold">
            Tour {tour.numero}/20 · <span className={sideTextClass(tour.side)}>{libelleTour(draft.tours, tour.numero)}</span>
          </p>
          <p className="text-sm text-muted-foreground">
            {consigne ??
              (aToi
                ? `À toi de ${tour.action === "ban" ? "bannir" : "choisir"} pour l'Équipe ${tour.equipe}`
                : `Au tour de l'Équipe ${tour.equipe}`)}
          </p>
        </div>
        <TempsRestant tour={tour} />
      </CardContent>
    </Card>
  );
}

function TempsRestant({ tour }: { tour: Schemas["TourCourantResponse"] }) {
  const secondes = secondesRestantes(tour, useMaintenant());

  return (
    <span
      aria-label="Temps restant"
      className={cn("flex items-center gap-2 text-2xl font-bold tabular-nums", secondes <= 10 && "text-destructive")}
    >
      <Timer className="size-5" />
      {secondes} s
    </span>
  );
}

function CoteDraft({
  side,
  equipe,
  tours,
  parId,
}: {
  side: "blue" | "red";
  equipe: string;
  tours: TourDeLaDraft[];
  parId: Map<string, Champion>;
}) {
  const duSide = tours.filter((t) => t.side === side);
  return (
    <Card role="group" aria-label={`Équipe ${equipe}, ${side} side`}>
      <CardHeader>
        <CardTitle className={sideTextClass(side)}>
          Équipe {equipe} · {side === "blue" ? "Blue side" : "Red side"}
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <div>
          <p className="mb-2 text-xs font-medium text-muted-foreground uppercase">Bans</p>
          <ul className="flex flex-wrap gap-2">
            {duSide
              .filter((t) => t.action === "ban")
              .map((t) => (
                <li key={t.numero}>
                  <CaseChampion tour={t} champion={t.championId ? parId.get(t.championId) : undefined} />
                </li>
              ))}
          </ul>
        </div>
        <div>
          <p className="mb-2 text-xs font-medium text-muted-foreground uppercase">Picks</p>
          <ul className="flex flex-wrap gap-2">
            {duSide
              .filter((t) => t.action === "pick")
              .map((t) => (
                <li key={t.numero}>
                  <CaseChampion tour={t} champion={t.championId ? parId.get(t.championId) : undefined} />
                </li>
              ))}
          </ul>
        </div>
      </CardContent>
    </Card>
  );
}

/** Un tour de la Draft : le champion joué, la case du tour courant, ou une case vide. */
function CaseChampion({ tour, champion }: { tour: TourDeLaDraft; champion?: Champion }) {
  const nom = champion?.name ?? tour.championId;
  return (
    <div
      title={`${tour.libelle}${nom ? ` : ${nom}` : ""}`}
      className={cn(
        "relative flex size-12 items-center justify-center overflow-hidden rounded-md border bg-muted text-[10px] text-muted-foreground",
        tour.courant && "border-primary ring-2 ring-primary",
      )}
    >
      {champion ? (
        <img
          src={champion.imageUrl}
          alt={champion.name}
          className={cn("size-full object-cover", tour.action === "ban" && "grayscale")}
        />
      ) : (
        (nom ?? tour.numero)
      )}
      {tour.action === "ban" && champion && <Ban aria-hidden className="absolute size-8 text-destructive/80" />}
    </div>
  );
}

function OrdreTournament({ tours, parId }: { tours: TourDeLaDraft[]; parId: Map<string, Champion> }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Ordre Tournament</CardTitle>
      </CardHeader>
      <CardContent>
        <ol aria-label="Ordre Tournament" className="flex flex-wrap gap-2">
          {tours.map((t) => (
            <li key={t.numero}>
              <Badge
                variant={t.courant ? "default" : "outline"}
                aria-current={t.courant ? "step" : undefined}
                className={cn(!t.courant && sideTextClass(t.side), !t.courant && !t.championId && "opacity-60")}
              >
                {t.numero}. {t.libelle}
                {t.championId && ` · ${parId.get(t.championId)?.name ?? t.championId}`}
              </Badge>
            </li>
          ))}
        </ol>
      </CardContent>
    </Card>
  );
}

/** Le champion du tour courant, choisi parmi ceux encore disponibles. */
function ChoixChampion({ draft, champions, jeu }: { draft: Draft; champions: Champion[]; jeu: JeuDuTour }) {
  const tour = draft.tourCourant!;
  const [recherche, setRecherche] = useState("");
  const [choisi, setChoisi] = useState<string | null>(null);
  const pris = championsPris(draft.actions);
  const verbe = tour.action === "ban" ? "Bannir" : "Choisir";
  const nomChoisi = choisi ? champions.find((c) => c.id === choisi)?.name : null;

  return (
    <Card className="border-primary">
      <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-4">
        <CardTitle>{jeu.titre ?? (tour.action === "ban" ? "Bannir un champion" : "Choisir un champion")}</CardTitle>
        <Button
          disabled={!choisi || pris.has(choisi) || jeu.pending}
          onClick={() => choisi && jeu.onJouer(tour.numero, choisi)}
        >
          {nomChoisi ? `${verbe} ${nomChoisi}` : verbe}
        </Button>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {jeu.error != null && <ErrorAlert error={jeu.error} />}
        <Input
          type="search"
          aria-label="Rechercher un champion"
          placeholder="Rechercher un champion"
          value={recherche}
          onChange={(e) => setRecherche(e.target.value)}
        />
        <ul className="grid max-h-96 grid-cols-[repeat(auto-fill,minmax(4.5rem,1fr))] gap-2 overflow-y-auto">
          {filtrerChampions(champions, recherche).map((c) => {
            const indisponible = pris.has(c.id);
            return (
              <li key={c.id}>
                <button
                  type="button"
                  disabled={indisponible}
                  aria-pressed={choisi === c.id}
                  onClick={() => setChoisi(c.id)}
                  className={cn(
                    "flex w-full flex-col items-center gap-1 rounded-md border p-1 text-xs hover:bg-accent disabled:cursor-not-allowed disabled:opacity-30 disabled:grayscale",
                    choisi === c.id && "border-primary bg-accent",
                  )}
                >
                  <img src={c.imageUrl} alt="" className="size-12 rounded" />
                  <span className="w-full truncate text-center">{c.name}</span>
                  {indisponible && <span className="sr-only">(indisponible)</span>}
                </button>
              </li>
            );
          })}
        </ul>
      </CardContent>
    </Card>
  );
}
