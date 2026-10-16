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
import { problemStatus } from "@/lib/problem";
import { cn } from "@/lib/utils";

type Draft = Schemas["DraftChampionsResponse"];
type Champion = Schemas["ChampionResponse"];
type Portee = Schemas["PorteeLien"];

/** Rafraîchissement de la Draft de champions (polling). */
const POLLING_DRAFT_MS = 1500;
/** Revalidation du lien : un lien renouvelé est refusé dès le prochain appel. */
const POLLING_LIEN_MS = 5000;

const sideClass = (side: string) => (side === "red" ? "text-red-400" : "text-sky-400");

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
  const champions = $api.useQuery("get", "/champions", {}, { staleTime: Infinity });

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
          lien={lien}
          equipe={equipe}
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

function DraftChampionsView({
  draft,
  champions,
  lien,
  equipe,
}: {
  draft: Draft;
  champions: Champion[];
  lien: string;
  /** L'Équipe du Lien capitaine ; null en lecture seule. */
  equipe: string | null;
}) {
  const parId = useMemo(() => new Map(champions.map((c) => [c.id, c])), [champions]);
  const tours = toursDeLaDraft(draft);
  const blue = draft.equipeBlue;
  const red = draft.equipeRed;

  return (
    <div className="flex flex-col gap-6">
      <EtatDuTour draft={draft} equipe={equipe} />

      <div className="grid gap-4 md:grid-cols-2">
        <CoteDraft side="blue" equipe={blue} tours={tours} parId={parId} />
        <CoteDraft side="red" equipe={red} tours={tours} parId={parId} />
      </div>

      {equipe && peutJouer(draft, equipe) && (
        <ChoixChampion key={draft.tourCourant!.numero} draft={draft} champions={champions} lien={lien} />
      )}

      <OrdreTournament tours={tours} parId={parId} />

      <div className="grid gap-4 md:grid-cols-2">
        <TeamCard equipe="A" players={draft.teamA} capitaine={draft.capitaineA} highlighted={equipe === "A"} />
        <TeamCard equipe="B" players={draft.teamB} capitaine={draft.capitaineB} highlighted={equipe === "B"} />
      </div>
      {!draft.terminee && (
        <p className="text-sm text-muted-foreground">Les rôles de l'Autorole s'afficheront à la fin de la Draft.</p>
      )}
    </div>
  );
}

function EtatDuTour({ draft, equipe }: { draft: Draft; equipe: string | null }) {
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
            Tour {tour.numero}/20 · <span className={sideClass(tour.side)}>{libelleTour(tour.numero)}</span>
          </p>
          <p className="text-sm text-muted-foreground">
            {aToi
              ? `À toi de ${tour.action === "ban" ? "bannir" : "choisir"} pour l'Équipe ${tour.equipe}`
              : `Au tour de l'Équipe ${tour.equipe}`}
          </p>
        </div>
        <TempsRestant tour={tour} />
      </CardContent>
    </Card>
  );
}

function TempsRestant({ tour }: { tour: Schemas["TourCourantResponse"] }) {
  const [maintenant, setMaintenant] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setMaintenant(Date.now()), 250);
    return () => clearInterval(id);
  }, []);
  const secondes = secondesRestantes(tour, maintenant);

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
        <CardTitle className={sideClass(side)}>
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
      title={`${libelleTour(tour.numero)}${nom ? ` : ${nom}` : ""}`}
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
                className={cn(!t.courant && sideClass(t.side), !t.courant && !t.championId && "opacity-60")}
              >
                {t.numero}. {libelleTour(t.numero)}
                {t.championId && ` · ${parId.get(t.championId)?.name ?? t.championId}`}
              </Badge>
            </li>
          ))}
        </ol>
      </CardContent>
    </Card>
  );
}

/** Le capitaine choisit le champion du tour courant parmi ceux encore disponibles. */
function ChoixChampion({ draft, champions, lien }: { draft: Draft; champions: Champion[]; lien: string }) {
  const tour = draft.tourCourant!;
  const queryClient = useQueryClient();
  const [recherche, setRecherche] = useState("");
  const [choisi, setChoisi] = useState<string | null>(null);
  const pris = championsPris(draft.actions);
  const jouer = $api.useMutation("post", "/liens/{lien}/draft-champions/actions", {
    onSuccess: (data) =>
      queryClient.setQueryData(["get", "/liens/{lien}/draft-champions", { params: { path: { lien } } }], data),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["get", "/liens/{lien}/draft-champions"] }),
  });
  const verbe = tour.action === "ban" ? "Bannir" : "Choisir";
  const nomChoisi = choisi ? champions.find((c) => c.id === choisi)?.name : null;

  return (
    <Card className="border-primary">
      <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-4">
        <CardTitle>{tour.action === "ban" ? "Bannir un champion" : "Choisir un champion"}</CardTitle>
        <Button
          disabled={!choisi || pris.has(choisi) || jouer.isPending}
          onClick={() => choisi && jouer.mutate({ params: { path: { lien } }, body: { tour: tour.numero, championId: choisi } })}
        >
          {nomChoisi ? `${verbe} ${nomChoisi}` : verbe}
        </Button>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {jouer.isError && <ErrorAlert error={jouer.error} />}
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
