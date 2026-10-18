import { useQuery, useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowRightIcon, LinkIcon, MoonIcon, RefreshCwIcon, SunIcon, UnlinkIcon } from "lucide-react";
import { useState, type ReactNode } from "react";
import { $api, type Schemas } from "@/api/client";
import { CourbeDuCost, DernieresGames } from "@/components/fiche-joueur";
import { ErrorAlert } from "@/components/query-state";
import { SelecteurDePeak, type Peak } from "@/components/selecteur-de-peak";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { COST_TIERS, costText, costTier, evolutionDuCost } from "@/lib/cost";
import { decimalLabel, entierLabel, pourcentLabel } from "@/lib/fiche";
import { matchesPlayer, myPlayerQuery } from "@/lib/players";
import { problemMessage } from "@/lib/problem";
import { normalizeTier, rankLabel, roleLabel } from "@/lib/ranks";
import { useTheme } from "@/lib/theme";
import { useDeconnexion } from "@/lib/use-deconnexion";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/_connecte/compte")({
  component: Compte,
});

type Player = Schemas["PlayerDto"];

const ABSENT = "–";

function Compte() {
  const myPlayer = useQuery(myPlayerQuery);

  return (
    <div className="flex flex-col gap-4">
      <CompteDiscord />
      {myPlayer.isPending ? (
        <Skeleton className="h-64 w-full" />
      ) : myPlayer.isError ? (
        <Alert variant="destructive">
          <AlertDescription>{problemMessage(myPlayer.error)}</AlertDescription>
        </Alert>
      ) : myPlayer.data ? (
        <ProfilDuJoueur player={myPlayer.data} />
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          <JoueurNonLie />
          <Preferences />
        </div>
      )}
    </div>
  );
}

/** Une carte nommée : repérée comme région par son titre. */
function Bloc({ titre, children, className, action }: { titre: string; children: ReactNode; className?: string; action?: ReactNode }) {
  return (
    <Card role="region" aria-label={titre} className={className}>
      <CardHeader className="flex flex-row items-center justify-between gap-2">
        <CardTitle>{titre}</CardTitle>
        {action}
      </CardHeader>
      <CardContent className="flex flex-col gap-3">{children}</CardContent>
    </Card>
  );
}

const COULEUR_HEX = /^#[0-9a-f]{6}$/i;

/** Photo, pseudo, bannière et rôle du Compte Discord, avec le rafraîchissement à la demande. */
function CompteDiscord() {
  const { user } = Route.useRouteContext();
  const role = user.role === "Superadministrateur" ? "Superadmin" : user.role === "Administrateur" ? "Admin" : null;
  const couleur = user.bannerColor && COULEUR_HEX.test(user.bannerColor) ? user.bannerColor : null;

  // Le rafraîchissement passe par l'API puis Discord : il demande une vraie navigation, pas un fetch.
  const rafraichir = () =>
    window.location.assign(`/api/auth/discord/refresh?returnUrl=${encodeURIComponent("/compte")}`);

  return (
    <Card className="gap-0 overflow-hidden py-0">
      <div className="h-24 bg-muted sm:h-28">
        {user.bannerUrl ? (
          <img src={user.bannerUrl} alt="Bannière Discord" className="size-full object-cover" />
        ) : (
          couleur && (
            <svg aria-hidden className="size-full">
              <rect width="100%" height="100%" fill={couleur} />
            </svg>
          )
        )}
      </div>
      <div className="-mt-9 flex flex-wrap items-end gap-4 px-5 pb-4">
        <Avatar className="size-20 border-4 border-card">
          {user.avatarUrl && <AvatarImage src={user.avatarUrl} alt="" />}
          <AvatarFallback className="text-2xl">{user.username?.charAt(0).toUpperCase()}</AvatarFallback>
        </Avatar>
        <div className="flex min-w-40 flex-1 flex-col gap-0.5">
          <h1 className="flex flex-wrap items-center gap-2 text-2xl font-bold">
            {user.username}
            {role && <Badge variant={role === "Superadmin" ? "default" : "secondary"}>{role}</Badge>}
          </h1>
          <p className="text-sm text-muted-foreground">Compte Discord</p>
        </div>
        <Button variant="outline" size="sm" onClick={rafraichir}>
          <RefreshCwIcon />
          Rafraîchir depuis Discord
        </Button>
      </div>
    </Card>
  );
}

/** Le Profil d'un Utilisateur lié : cost et customs d'abord, SoloQ discrète sous le Joueur lié. */
function ProfilDuJoueur({ player }: { player: Player }) {
  const fiche = $api.useQuery("get", "/players/{playerId}/fiche", { params: { path: { playerId: player.id } } });

  return (
    <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,20rem)_minmax(0,1fr)]">
      <div className="flex min-w-0 flex-col gap-4">
        <MonCost player={player} />
        <MesCustoms playerId={player.id} fiche={fiche} />
      </div>
      <div className="flex min-w-0 flex-col gap-4">
        {fiche.data ? <DernieresGames games={fiche.data.games} /> : null}
        <JoueurLie player={player} />
        <Preferences />
      </div>
    </div>
  );
}

function MonCost({ player }: { player: Player }) {
  const tier = costTier(player.currentCost);
  const evolution = evolutionDuCost(player.costEvolution);

  return (
    <Bloc titre="Mon cost">
      <div className="flex items-center gap-4">
        <span
          aria-label={`Tier ${tier}`}
          role="img"
          className="flex size-14 shrink-0 items-center justify-center rounded-xl bg-primary text-3xl font-bold text-primary-foreground"
        >
          {tier}
        </span>
        <div className="flex flex-col">
          <span className="text-3xl leading-none font-bold tabular-nums">{costText(player.currentCost)}</span>
          <span className="text-sm text-muted-foreground">
            {evolution ? (
              <span role="img" aria-label={evolution.label}>
                {evolution.texte}
              </span>
            ) : (
              ABSENT
            )}
          </span>
        </div>
      </div>
      <ol aria-label="Échelle des tiers" className="flex gap-1">
        {[...COST_TIERS].reverse().map((t) => (
          <li
            key={t}
            aria-current={t === tier ? "true" : undefined}
            className={cn(
              "flex-1 rounded-sm bg-muted py-1 text-center font-mono text-xs font-semibold text-muted-foreground",
              t === tier && "bg-primary text-primary-foreground",
            )}
          >
            {t}
          </li>
        ))}
      </ol>
      <CourbeDuCost playerId={player.id} />
    </Bloc>
  );
}

function Ligne({ libelle, children }: { libelle: string; children: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-3 text-sm">
      <dt className="text-muted-foreground">{libelle}</dt>
      <dd className="text-right font-semibold tabular-nums">{children}</dd>
    </div>
  );
}

function MesCustoms({
  playerId,
  fiche,
}: {
  playerId: number;
  fiche: { isPending: boolean; isError: boolean; error: unknown; data?: Schemas["FicheJoueur"] };
}) {
  return (
    <Bloc titre="Mes customs">
      {fiche.isPending ? (
        <Skeleton className="h-40 w-full" />
      ) : fiche.isError || !fiche.data ? (
        <ErrorAlert error={fiche.error} />
      ) : (
        <>
          <dl className="flex flex-col gap-2">
            <Ligne libelle="Games">{fiche.data.stats.gamesJouees}</Ligne>
            <Ligne libelle="Winrate">{pourcentLabel(fiche.data.stats.winrate)}</Ligne>
            <Ligne libelle="KDA">{decimalLabel(fiche.data.stats.kda)}</Ligne>
            <Ligne libelle="Note moyenne">
              {fiche.data.stats.noteMoyenne == null ? ABSENT : `${entierLabel(fiche.data.stats.noteMoyenne)} / 100`}
            </Ligne>
            <Ligne libelle="Participation aux kills">{pourcentLabel(fiche.data.stats.participationAuxKills)}</Ligne>
            <Ligne libelle="MVP · DOFD">
              {fiche.data.titres.mvp} · {fiche.data.titres.dofd}
            </Ligne>
          </dl>
          <Link
            to="/joueurs/$playerId"
            params={{ playerId: String(playerId) }}
            className="inline-flex items-center gap-1 text-sm font-medium underline underline-offset-4"
          >
            Voir ma fiche joueur
            <ArrowRightIcon className="size-4" />
          </Link>
        </>
      )}
    </Bloc>
  );
}

/** Le Joueur lié : RiotId, changement et déliage ; la SoloQ (rang, rôle principal, peak) reste discrète. */
function JoueurLie({ player }: { player: Player }) {
  const [changement, setChangement] = useState(false);
  const [delier, setDelier] = useState(false);
  const [peakOuvert, setPeakOuvert] = useState(false);
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: myPlayerQuery.queryKey });
  const unlink = $api.useMutation("delete", "/players/link", {
    onSuccess: async () => {
      await refresh();
      setDelier(false);
    },
  });
  const peak = $api.useMutation("post", "/players/peak", {
    onSuccess: async () => {
      await refresh();
      setPeakOuvert(false);
    },
  });

  const rang = player.rankTier ? rankLabel(player.rankTier, player.rankDivision, player.lp) : ABSENT;
  const role = roleLabel(player.mainRole) ?? ABSENT;
  const aUnPeak = normalizeTier(player.peakTier) !== "UNRANKED";

  return (
    <>
      <Bloc
        titre="Joueur lié"
        action={
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" size="sm" onClick={() => setChangement((v) => !v)}>
              <LinkIcon />
              Changer de joueur
            </Button>
            <Button variant="outline" size="sm" className="text-destructive" onClick={() => setDelier(true)}>
              <UnlinkIcon />
              Délier
            </Button>
          </div>
        }
      >
        <p className="font-mono text-sm font-semibold">{player.riotId}</p>
        <dl className="flex flex-col gap-2 border-t pt-3 text-sm text-muted-foreground">
          <div className="flex items-center justify-between gap-3">
            <dt>SoloQ</dt>
            <dd className="text-foreground">{rang}</dd>
          </div>
          <div className="flex items-center justify-between gap-3">
            <dt>Rôle principal</dt>
            <dd className="text-foreground">{role}</dd>
          </div>
          <div className="flex items-center justify-between gap-3">
            <dt>Peak</dt>
            <dd className="flex items-center gap-2 text-foreground">
              {aUnPeak ? (
                <>
                  <span>{rankLabel(player.peakTier, player.peakDivision, player.peakLp)}</span>
                  {player.peakSeason && <Badge variant="outline">{player.peakSeason}</Badge>}
                </>
              ) : (
                ABSENT
              )}
              {!peakOuvert && (
                <Button variant="ghost" size="sm" onClick={() => setPeakOuvert(true)}>
                  Régler mon peak
                </Button>
              )}
            </dd>
          </div>
        </dl>
        {peakOuvert && (
          <SelecteurDePeak
            initial={aUnPeak ? (player as Peak) : null}
            enCours={peak.isPending}
            erreur={peak.error}
            onAnnuler={() => setPeakOuvert(false)}
            onValider={(valeur) => peak.mutate({ body: valeur })}
          />
        )}
      </Bloc>
      {changement && <FormulaireDeLiaison joueurLieId={player.id} onLie={() => setChangement(false)} />}
      <Dialog open={delier} onOpenChange={setDelier}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Délier mon joueur ?</DialogTitle>
            <DialogDescription>
              Seul le lien avec {player.prenom} disparaît. Le joueur et ses stats restent, ton compte Discord aussi, et tu
              pourras te relier ensuite.
            </DialogDescription>
          </DialogHeader>
          {unlink.isError && (
            <Alert variant="destructive">
              <AlertDescription>{problemMessage(unlink.error)}</AlertDescription>
            </Alert>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setDelier(false)}>
              Annuler
            </Button>
            <Button variant="destructive" disabled={unlink.isPending} onClick={() => unlink.mutate({})}>
              Délier
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

/** Un utilisateur sans Joueur lié : l'invitation à lier son compte. */
function JoueurNonLie() {
  return (
    <section aria-label="Joueur lié" className="flex flex-col gap-4">
      <Card>
        <CardHeader>
          <CardTitle>Joueur lié</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground">Aucun joueur n'est lié à ton compte Discord.</p>
        </CardContent>
      </Card>
      <FormulaireDeLiaison />
    </section>
  );
}

function FormulaireDeLiaison({ joueurLieId, onLie }: { joueurLieId?: number; onLie?: () => void }) {
  const [search, setSearch] = useState("");
  const queryClient = useQueryClient();
  const players = $api.useQuery("get", "/players");
  const link = $api.useMutation("post", "/players/link", {
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: myPlayerQuery.queryKey });
      onLie?.();
    },
  });

  const candidates = (players.data ?? []).filter((p) => p.id !== joueurLieId && matchesPlayer(p, search));

  return (
    <Card>
      <CardHeader>
        <CardTitle>{joueurLieId ? "Changer de joueur" : "Lier mon compte à un joueur"}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <Input
          type="search"
          aria-label="Chercher un joueur"
          placeholder="Prénom ou RiotId"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        {link.isError && (
          <Alert variant="destructive">
            <AlertDescription>{problemMessage(link.error)}</AlertDescription>
          </Alert>
        )}
        {players.isError && (
          <Alert variant="destructive">
            <AlertDescription>{problemMessage(players.error)}</AlertDescription>
          </Alert>
        )}
        <ul className="flex max-h-80 flex-col divide-y overflow-y-auto">
          {candidates.map((p) => (
            <li key={p.id} className="flex items-center justify-between gap-4 py-2">
              <div>
                <p className="font-medium">{p.prenom}</p>
                <p className="text-xs text-muted-foreground">{p.riotId}</p>
              </div>
              <Button
                size="sm"
                variant="outline"
                aria-label={`Lier ${p.prenom}`}
                disabled={link.isPending}
                onClick={() => link.mutate({ body: { playerId: p.id } })}
              >
                Lier
              </Button>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

function Preferences() {
  const [theme, setTheme] = useTheme();
  const logout = useDeconnexion();

  return (
    <Bloc titre="Préférences">
      <div className="flex items-center justify-between gap-3 text-sm">
        <span className="text-muted-foreground">Thème</span>
        <div className="flex gap-1 rounded-md bg-muted p-0.5">
          <Button size="sm" variant={theme === "dark" ? "outline" : "ghost"} aria-pressed={theme === "dark"} onClick={() => setTheme("dark")}>
            <MoonIcon />
            Sombre
          </Button>
          <Button size="sm" variant={theme === "light" ? "outline" : "ghost"} aria-pressed={theme === "light"} onClick={() => setTheme("light")}>
            <SunIcon />
            Clair
          </Button>
        </div>
      </div>
      <div className="flex items-center justify-between gap-3 text-sm">
        <span className="text-muted-foreground">Session</span>
        <Button variant="outline" size="sm" disabled={logout.isPending} onClick={() => logout.mutate({})}>
          Se déconnecter
        </Button>
      </div>
    </Bloc>
  );
}
