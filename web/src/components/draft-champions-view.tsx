import { useQueryClient } from "@tanstack/react-query";
import { Ban, History, Pause, Timer, Volume2, VolumeX } from "lucide-react";
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
  formaterDecompte,
  libelleTour,
  peutJouer,
  secondesRestantes,
  toursDeLaDraft,
  VOIES,
  type TourDeLaDraft,
  type Voie,
} from "@/lib/draft-champions";
import { POLLING_DRAFT_MS, useChampions } from "@/lib/draft-champions-queries";
import { useSonsDeLaDraft } from "@/lib/draft-sons";
import { problemStatus } from "@/lib/problem";
import { sideLabel } from "@/lib/series";
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
export function DraftParLien({
  lien,
  portee,
}: {
  lien: string;
  portee: Portee;
}) {
  const path = { params: { path: { lien } } };
  const acces = $api.useQuery("get", "/liens/{lien}", path, {
    refetchInterval: POLLING_LIEN_MS,
    retry: false,
  });
  const draft = $api.useQuery("get", "/liens/{lien}/draft-champions", path, {
    refetchInterval: POLLING_DRAFT_MS,
    retry: false,
    enabled: acces.isSuccess,
  });
  const champions = useChampions();
  const queryClient = useQueryClient();
  const jouer = $api.useMutation(
    "post",
    "/liens/{lien}/draft-champions/actions",
    {
      onSuccess: (data) =>
        queryClient.setQueryData(
          ["get", "/liens/{lien}/draft-champions", path],
          data,
        ),
      onSettled: () =>
        queryClient.invalidateQueries({
          queryKey: ["get", "/liens/{lien}/draft-champions"],
        }),
    },
  );

  const rafraichir = () => {
    queryClient.invalidateQueries({
      queryKey: ["get", "/liens/{lien}/draft-champions"],
    });
    queryClient.invalidateQueries({ queryKey: ["get", "/liens/{lien}"] });
  };
  const demanderReset = $api.useMutation(
    "post",
    "/liens/{lien}/draft-champions/reset",
    { onSettled: rafraichir },
  );
  const repondreReset = $api.useMutation(
    "post",
    "/liens/{lien}/draft-champions/reset/reponse",
    {
      onSettled: rafraichir,
    },
  );

  const surEtat = {
    onSuccess: (data: Draft) => queryClient.setQueryData(["get", "/liens/{lien}/draft-champions", path], data),
    onSettled: () =>
      queryClient.invalidateQueries({
        queryKey: ["get", "/liens/{lien}/draft-champions"],
      }),
  };
  const pause = $api.useMutation("post", "/liens/{lien}/draft-champions/pause", surEtat);
  const reprise = $api.useMutation("post", "/liens/{lien}/draft-champions/reprise", surEtat);
  const acceptation = $api.useMutation("post", "/liens/{lien}/draft-champions/reprise/acceptation", surEtat);
  const refus = $api.useMutation("post", "/liens/{lien}/draft-champions/reprise/refus", surEtat);

  const pret = $api.useMutation("put", "/liens/{lien}/pret", {
    onSuccess: (data) =>
      queryClient.setQueryData(["get", "/liens/{lien}", path], data),
  });

  const draftOuverte =
    draft.isError && problemStatus(draft.error) === 404
      ? null
      : draft.data;
  const equipeDuLien =
    portee === "Capitaine" && acces.data?.portee === "Capitaine"
      ? acces.data.equipe
      : null;
  const sons = useSonsDeLaDraft(draftOuverte, equipeDuLien);

  const titre =
    portee === "Capitaine"
      ? "Draft de champions — capitaine"
      : "Draft de champions — spectateur";
  const meta = draft.data
    ? `Série ${draft.data.seriesId} · Game ${draft.data.gameId}${draft.data.fearless ? " · Fearless" : ""}`
    : undefined;

  if (acces.isError) {
    return (
      <Page titre={titre}>
        {problemStatus(acces.error) === 404 ? (
          <LienInvalide />
        ) : (
          <ErrorAlert error={acces.error} />
        )}
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
  const equipe =
    portee === "Capitaine" && acces.data.portee === "Capitaine"
      ? acces.data.equipe
      : null;

  return (
    <Page
      titre={titre}
      meta={meta}
      sons={sons}
      badge={
        equipe
          ? `Tu es le capitaine de l'Équipe ${equipe}`
          : "En direct · lecture seule"
      }
    >
      {draft.isPending ? (
        <LoadingRows />
      ) : draft.isError &&
        (!draft.data || problemStatus(draft.error) === 404) ? (
        problemStatus(draft.error) === 404 ? (
          <div className="flex flex-col gap-4">
            {draft.data && (
              <Alert>
                <AlertTitle>Draft remise à zéro</AlertTitle>
                <AlertDescription>
                  Les bans, les picks et le chrono sont effacés.
                </AlertDescription>
              </Alert>
            )}
            <p className="text-muted-foreground">
              La Draft de champions n'est pas encore ouverte : elle démarre
              toute seule quand les deux Capitaines sont prêts.
            </p>
            {equipe && (
              <PretDuCapitaine
                pret={acces.data.pret}
                adversairePret={acces.data.adversairePret}
                enCours={pret.isPending}
                erreur={pret.isError ? pret.error : null}
                onChange={(valeur) =>
                  pret.mutate({ ...path, body: { pret: valeur } })
                }
              />
            )}
          </div>
        ) : (
          <ErrorAlert error={draft.error} />
        )
      ) : (
        <div className="flex flex-col gap-6">
          <ResetDeLaDraft
            draft={draft.data!}
            equipe={equipe}
            enCours={demanderReset.isPending || repondreReset.isPending}
            erreur={demanderReset.error ?? repondreReset.error}
            onDemander={() => demanderReset.mutate(path)}
            onRepondre={(accepte) =>
              repondreReset.mutate({ ...path, body: { accepte } })
            }
          />
          {equipe && !draft.data!.terminee && (
            <PauseDuCapitaine
              draft={draft.data!}
              equipe={equipe}
              enCours={
                pause.isPending ||
                reprise.isPending ||
                acceptation.isPending ||
                refus.isPending
              }
              erreur={
                pause.error ?? reprise.error ?? acceptation.error ?? refus.error
              }
              onPause={() => pause.mutate(path)}
              onDemanderReprise={() => reprise.mutate(path)}
              onAccepter={() => acceptation.mutate(path)}
              onRefuser={() => refus.mutate(path)}
            />
          )}
          <DraftChampionsView
            draft={draft.data!}
            champions={champions.data ?? []}
            equipe={equipe}
            spectateur={equipe === null}
            jeu={
              equipe && peutJouer(draft.data!, equipe)
                ? {
                    pending: jouer.isPending,
                    error: jouer.isError ? jouer.error : null,
                    onJouer: (tour, championId) =>
                      jouer.mutate({ ...path, body: { tour, championId } }),
                  }
                : null
            }
          />
        </div>
      )}
      {champions.isError && <ErrorAlert error={champions.error} />}
    </Page>
  );
}

/**
 * Reset de la draft : le Capitaine demande, l'autre accepte ou refuse sous 60 s ; le spectateur voit
 * seulement « Reset demandé ». Rien une fois la draft terminée.
 */
function ResetDeLaDraft({
  draft,
  equipe,
  enCours,
  erreur,
  onDemander,
  onRepondre,
}: {
  draft: Draft;
  equipe: string | null;
  enCours: boolean;
  erreur: unknown;
  onDemander: () => void;
  onRepondre: (accepte: boolean) => void;
}) {
  const maintenant = useMaintenant(1000);
  if (draft.terminee) return null;

  const demande = draft.resetDemande;
  const secondes = demande
    ? Math.max(0, Math.ceil((Date.parse(demande.expireLe) - maintenant) / 1000))
    : 0;

  if (!demande || secondes === 0) {
    return equipe ? (
      <div className="flex flex-col items-start gap-2">
        <Button variant="outline" disabled={enCours} onClick={onDemander}>
          Demander un Reset de la draft
        </Button>
        {erreur != null && <ErrorAlert error={erreur} />}
      </div>
    ) : null;
  }

  const peutRepondre = equipe !== null && equipe !== demande.equipe;
  const texte =
    equipe === demande.equipe
      ? "En attente de la réponse de l'autre Capitaine."
      : `L'Équipe ${demande.equipe} demande à remettre la draft à zéro.`;
  return (
    <Alert role="status">
      <AlertTitle>Reset demandé</AlertTitle>
      <AlertDescription className="flex flex-col items-start gap-2">
        <span>
          {texte} Sans réponse dans {secondes} s, rien ne change.
        </span>
        {peutRepondre && (
          <span className="flex gap-2">
            <Button disabled={enCours} onClick={() => onRepondre(true)}>
              Accepter le Reset
            </Button>
            <Button
              variant="outline"
              disabled={enCours}
              onClick={() => onRepondre(false)}
            >
              Refuser le Reset
            </Button>
          </span>
        )}
        {erreur != null && <ErrorAlert error={erreur} />}
      </AlertDescription>
    </Alert>
  );
}

/** « Je suis prêt » : le Capitaine se met prêt, et peut se raviser tant que l'autre ne l'est pas. */
function PretDuCapitaine({
  pret,
  adversairePret,
  enCours,
  erreur,
  onChange,
}: {
  pret: boolean;
  adversairePret: boolean;
  enCours: boolean;
  erreur: unknown;
  onChange: (pret: boolean) => void;
}) {
  return (
    <Card>
      <CardContent className="flex flex-col items-start gap-3">
        <Button
          size="lg"
          variant={pret ? "outline" : "default"}
          disabled={enCours}
          onClick={() => onChange(!pret)}
        >
          {pret ? "Je ne suis plus prêt" : "Je suis prêt"}
        </Button>
        <p role="status" className="text-sm text-muted-foreground">
          {adversairePret
            ? "L'autre Capitaine est prêt."
            : "L'autre Capitaine n'est pas encore prêt."}
        </p>
        {erreur != null && <ErrorAlert error={erreur} />}
      </CardContent>
    </Card>
  );
}

/** Pause : le Capitaine la met tout de suite, une fois ; la reprise anticipée demande l'accord de l'autre. */
function PauseDuCapitaine({
  draft,
  equipe,
  enCours,
  erreur,
  onPause,
  onDemanderReprise,
  onAccepter,
  onRefuser,
}: {
  draft: Draft;
  equipe: string;
  enCours: boolean;
  erreur: unknown;
  onPause: () => void;
  onDemanderReprise: () => void;
  onAccepter: () => void;
  onRefuser: () => void;
}) {
  const demande = draft.repriseDemandee;
  const disponible = equipe === "A" ? draft.pauseDisponibleA : draft.pauseDisponibleB;
  if (!draft.pause && !disponible) return null;

  return (
    <div className="flex flex-wrap items-center gap-3">
      {!draft.pause ? (
        <Button variant="outline" disabled={enCours} onClick={onPause}>
          <Pause />
          Mettre en pause
        </Button>
      ) : !demande ? (
        <Button disabled={enCours} onClick={onDemanderReprise}>
          Demander la reprise
        </Button>
      ) : demande.parEquipe === equipe ? (
        <p role="status" className="text-sm text-muted-foreground">
          En attente de l'autre Capitaine pour reprendre…
        </p>
      ) : (
        <>
          <p role="status" className="text-sm font-medium">
            L'autre Capitaine demande la reprise.
          </p>
          <Button disabled={enCours} onClick={onAccepter}>
            Accepter
          </Button>
          <Button variant="outline" disabled={enCours} onClick={onRefuser}>
            Refuser
          </Button>
        </>
      )}
      {erreur != null && <ErrorAlert error={erreur} />}
    </div>
  );
}

function BoutonSon({ coupe, basculer }: { coupe: boolean; basculer: () => void }) {
  const Icone = coupe ? VolumeX : Volume2;
  return (
    <Button variant="outline" size="sm" onClick={basculer}>
      <Icone className="size-4" />
      {coupe ? "Remettre le son" : "Couper le son"}
    </Button>
  );
}

function Page({
  titre,
  meta,
  badge,
  sons,
  children,
}: {
  titre: string;
  meta?: string;
  badge?: string;
  sons?: ReturnType<typeof useSonsDeLaDraft>;
  children: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold">{titre}</h1>
          {meta && <p className="mt-1 text-sm text-muted-foreground">{meta}</p>}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {sons?.disponible && <BoutonSon coupe={sons.coupe} basculer={sons.basculer} />}
          {badge && <Badge variant="outline">{badge}</Badge>}
        </div>
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
        Il est inconnu, a été renouvelé par l'organisateur, ou le Lien
        spectateur n'est pas encore disponible. Demande le lien à jour à
        l'organisateur.
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
  spectateur = false,
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
  /** Vue en lecture seule du lien public : le tour annoncé en grand, sans grille de champions. */
  spectateur?: boolean;
}) {
  const parId = useMemo(
    () => new Map(champions.map((c) => [c.id, c])),
    [champions],
  );
  const tours = toursDeLaDraft(draft);
  const blue = draft.equipeBlue;
  const red = draft.equipeRed;

  return (
    <div className="flex flex-col gap-6">
      <EtatDuTour
        draft={draft}
        equipe={equipe}
        consigne={consigne}
        spectateur={spectateur}
      />

      {spectateur ? (
        <div className="grid gap-4 md:grid-cols-2">
          <CoteDraft side="blue" equipe={blue} tours={tours} parId={parId} />
          <CoteDraft side="red" equipe={red} tours={tours} parId={parId} />
        </div>
      ) : (
        <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,2fr)_minmax(0,1fr)]">
          <CoteDraft side="blue" equipe={blue} tours={tours} parId={parId} />
          <div className="order-last flex flex-col gap-4 lg:order-none">
            {(jeu || equipe !== null) &&
              !draft.terminee &&
              draft.tourCourant && (
                <ChoixChampion
                  key={draft.tourCourant.numero}
                  draft={draft}
                  champions={champions}
                  jeu={jeu}
                />
              )}
          </div>
          <CoteDraft side="red" equipe={red} tours={tours} parId={parId} />
        </div>
      )}

      {draft.championsDejaJoues && (
        <DejaJoues championIds={draft.championsDejaJoues} parId={parId} />
      )}

      <OrdreTournament tours={tours} parId={parId} />

      {avecEquipes && (
        <>
          <div className="grid gap-4 md:grid-cols-2">
            <TeamCard
              equipe="A"
              players={draft.teamA}
              capitaine={draft.capitaineA}
              highlighted={equipe === "A"}
            />
            <TeamCard
              equipe="B"
              players={draft.teamB}
              capitaine={draft.capitaineB}
              highlighted={equipe === "B"}
            />
          </div>
          {!draft.terminee && (
            <p className="text-sm text-muted-foreground">
              Les rôles de l'Autorole s'afficheront à la fin de la Draft.
            </p>
          )}
        </>
      )}
    </div>
  );
}

function EtatDuTour({
  draft,
  equipe,
  consigne,
  spectateur,
}: {
  draft: Draft;
  equipe: string | null;
  consigne?: string;
  spectateur: boolean;
}) {
  const tour = draft.tourCourant;
  if (draft.terminee || !tour) {
    return (
      <Alert>
        <AlertTitle>Draft terminée</AlertTitle>
        <AlertDescription>
          Les champions et les rôles de la game sont fixés.
        </AlertDescription>
      </Alert>
    );
  }
  if (draft.pause) return <EnPause pause={draft.pause} />;

  const aToi = equipe !== null && tour.equipe === equipe;
  const verbe = tour.action === "ban" ? "bannir" : "choisir";
  return (
    <Card className={cn(aToi && "border-primary")}>
      <CardContent className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-col gap-1">
          {aToi && (
            <span className="inline-flex items-center gap-2 self-start rounded-full bg-primary px-3 py-1 text-sm font-semibold text-primary-foreground">
              <span
                aria-hidden
                className="size-2 rounded-full bg-primary-foreground motion-safe:animate-pulse"
              />
              À toi de {verbe}
            </span>
          )}
          {spectateur && (
            <h2 className="text-2xl font-bold">
              L'Équipe {tour.equipe}{" "}
              {tour.action === "ban" ? "bannit" : "choisit"}
            </h2>
          )}
          <p aria-label="Tour courant" className="text-lg font-semibold">
            Tour {tour.numero}/20 ·{" "}
            <span className={sideTextClass(tour.side)}>
              {libelleTour(draft.tours, tour.numero)}
            </span>
          </p>
          <p className="text-sm text-muted-foreground">
            {consigne ??
              (aToi
                ? `Équipe ${tour.equipe}, c'est ton tour`
                : `Au tour de l'Équipe ${tour.equipe}`)}
          </p>
        </div>
        <TempsRestant tour={tour} />
      </CardContent>
    </Card>
  );
}

/** La draft est en pause : le temps restant du tour est gelé, la reprise automatique approche. */
function EnPause({ pause }: { pause: NonNullable<Draft["pause"]> }) {
  const reprise = Math.max(0, Math.ceil((Date.parse(pause.finLe) - useMaintenant()) / 1000));

  return (
    <Card className="border-amber-500/60">
      <CardContent className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h2 className="flex items-center gap-2 text-2xl font-bold">
            <Pause className="size-6" />
            Draft en pause
          </h2>
          <p className="text-sm text-muted-foreground">
            Pause demandée par le Capitaine de l'Équipe {pause.parEquipe}.
          </p>
          <p className="text-sm">
            Reprise automatique dans{" "}
            <span aria-label="Reprise automatique dans" className="font-semibold tabular-nums">
              {formaterDecompte(reprise)}
            </span>
          </p>
        </div>
        <span aria-label="Temps restant du tour" className="flex items-center gap-2 text-2xl font-bold tabular-nums">
          <Timer className="size-5" />
          {Math.ceil(pause.tempsRestantTourMs / 1000)} s
        </span>
      </CardContent>
    </Card>
  );
}

function TempsRestant({ tour }: { tour: Schemas["TourCourantResponse"] }) {
  const secondes = secondesRestantes(tour, useMaintenant());

  return (
    <span
      aria-label="Temps restant"
      className={cn(
        "flex items-center gap-2 text-2xl font-bold tabular-nums",
        secondes <= 10 && "text-destructive",
      )}
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
          Équipe {equipe} · {sideLabel(side)}
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <div>
          <p className="mb-2 text-xs font-medium text-muted-foreground uppercase">
            Bans
          </p>
          <ul className="flex flex-wrap gap-2">
            {duSide
              .filter((t) => t.action === "ban")
              .map((t) => (
                <li key={t.numero}>
                  <CaseChampion
                    tour={t}
                    champion={
                      t.championId ? parId.get(t.championId) : undefined
                    }
                  />
                </li>
              ))}
          </ul>
        </div>
        <div>
          <p className="mb-2 text-xs font-medium text-muted-foreground uppercase">
            Picks
          </p>
          <ul className="flex flex-wrap gap-2">
            {duSide
              .filter((t) => t.action === "pick")
              .map((t) => (
                <li key={t.numero}>
                  <CaseChampion
                    tour={t}
                    champion={
                      t.championId ? parId.get(t.championId) : undefined
                    }
                  />
                </li>
              ))}
          </ul>
        </div>
      </CardContent>
    </Card>
  );
}

/** Un tour de la Draft : le champion joué, la case du tour courant, ou une case vide. */
function CaseChampion({
  tour,
  champion,
}: {
  tour: TourDeLaDraft;
  champion?: Champion;
}) {
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
          className={cn(
            "size-full object-cover",
            tour.action === "ban" && "grayscale",
          )}
        />
      ) : (
        (nom ?? tour.numero)
      )}
      {tour.action === "ban" && champion && (
        <Ban aria-hidden className="absolute size-8 text-destructive/80" />
      )}
    </div>
  );
}

/**
 * Fearless : les champions réellement joués dans les games précédentes de la Série. Simple rappel,
 * ils restent bannissables et pickables.
 */
function DejaJoues({
  championIds,
  parId,
}: {
  championIds: string[];
  parId: Map<string, Champion>;
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <History className="size-5" />
          Déjà joués dans la Série (Fearless)
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <p className="text-sm text-muted-foreground">
          Champions joués dans les games précédentes : l'app les signale, elle
          ne les bloque pas.
        </p>
        {championIds.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Aucun champion joué pour l'instant.
          </p>
        ) : (
          <ul
            aria-label="Champions déjà joués (Fearless)"
            className="flex flex-wrap gap-2"
          >
            {championIds.map((id) => {
              const champion = parId.get(id);
              return (
                <li
                  key={id}
                  className="flex items-center gap-2 rounded-md border border-amber-500/60 py-1 pr-2 pl-1 text-sm"
                >
                  {champion && (
                    <img
                      src={champion.imageUrl}
                      alt=""
                      className="size-6 rounded"
                    />
                  )}
                  <span>{champion?.name ?? id}</span>
                </li>
              );
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

function OrdreTournament({
  tours,
  parId,
}: {
  tours: TourDeLaDraft[];
  parId: Map<string, Champion>;
}) {
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
                className={cn(
                  !t.courant && sideTextClass(t.side),
                  !t.courant && !t.championId && "opacity-60",
                )}
              >
                {t.numero}. {t.libelle}
                {t.championId &&
                  ` · ${parId.get(t.championId)?.name ?? t.championId}`}
              </Badge>
            </li>
          ))}
        </ol>
      </CardContent>
    </Card>
  );
}

/** Le champion du tour courant, choisi parmi ceux encore disponibles. */
function ChoixChampion({
  draft,
  champions,
  jeu,
}: {
  draft: Draft;
  champions: Champion[];
  jeu: JeuDuTour | null;
}) {
  const tour = draft.tourCourant!;
  const [recherche, setRecherche] = useState("");
  const [voie, setVoie] = useState<Voie | null>(null);
  const [choisi, setChoisi] = useState<string | null>(null);
  const pris = championsPris(draft.actions);
  const dejaJoues = new Set(draft.championsDejaJoues ?? []);
  const verbe = tour.action === "ban" ? "Bannir" : "Choisir";
  const nomChoisi = choisi
    ? champions.find((c) => c.id === choisi)?.name
    : null;

  return (
    <Card className={cn(jeu && "border-primary")}>
      <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-4">
        <CardTitle>
          {jeu
            ? (jeu.titre ??
              (tour.action === "ban"
                ? "Bannir un champion"
                : "Choisir un champion"))
            : "Champions"}
        </CardTitle>
        {jeu && (
          <Button
            variant={tour.action === "ban" ? "destructive" : "default"}
            disabled={!choisi || pris.has(choisi) || jeu.pending}
            onClick={() => choisi && jeu.onJouer(tour.numero, choisi)}
          >
            {nomChoisi ? `${verbe} ${nomChoisi}` : verbe}
          </Button>
        )}
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {jeu?.error != null && <ErrorAlert error={jeu.error} />}
        <Input
          type="search"
          aria-label="Rechercher un champion"
          placeholder="Rechercher un champion"
          value={recherche}
          onChange={(e) => setRecherche(e.target.value)}
        />
        <div
          role="group"
          aria-label="Filtrer par voie"
          className="flex flex-wrap gap-2"
        >
          {[{ id: null, libelle: "Tous" }, ...VOIES].map((v) => (
            <Button
              key={v.libelle}
              type="button"
              size="sm"
              variant={voie === v.id ? "default" : "outline"}
              aria-pressed={voie === v.id}
              onClick={() => setVoie(v.id)}
            >
              {v.libelle}
            </Button>
          ))}
        </div>
        <p className="text-xs text-muted-foreground">
          Les champions grisés sont bannis. Ceux marqués « Joué » le sont par
          Fearless : ils restent affichés.
        </p>
        <ul className="grid max-h-96 grid-cols-[repeat(auto-fill,minmax(4.5rem,1fr))] gap-2 overflow-y-auto">
          {filtrerChampions(champions, recherche, voie).map((c) => {
            const indisponible = pris.has(c.id);
            const dejaJoue = dejaJoues.has(c.id);
            return (
              <li key={c.id}>
                <button
                  type="button"
                  disabled={indisponible || !jeu}
                  aria-pressed={choisi === c.id}
                  onClick={() => setChoisi(c.id)}
                  className={cn(
                    "flex w-full flex-col items-center gap-1 rounded-md border p-1 text-xs hover:bg-accent disabled:cursor-not-allowed disabled:opacity-30 disabled:grayscale",
                    dejaJoue && "border-amber-500/60",
                    choisi === c.id && "border-primary bg-accent",
                  )}
                >
                  <span className="relative">
                    <img src={c.imageUrl} alt="" className="size-12 rounded" />
                    {dejaJoue && (
                      <History
                        aria-hidden
                        className="absolute -top-1 -right-1 size-4 rounded-full bg-background text-amber-500"
                      />
                    )}
                  </span>
                  <span className="w-full truncate text-center">{c.name}</span>
                  {dejaJoue && (
                    <Badge
                      variant="outline"
                      className="border-amber-500/60 px-1 py-0 text-[10px]"
                    >
                      Joué
                    </Badge>
                  )}
                  {dejaJoue && (
                    <span className="sr-only">(déjà joué dans la Série)</span>
                  )}
                  {indisponible && (
                    <span className="sr-only">(indisponible)</span>
                  )}
                </button>
              </li>
            );
          })}
        </ul>
      </CardContent>
    </Card>
  );
}
