import { useQuery, useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowRightIcon, LinkIcon, MoonIcon, RefreshCwIcon, SunIcon, UnlinkIcon } from "lucide-react";
import { useState, type ReactNode } from "react";
import { $api, type Schemas } from "@/api/client";
import { BanniereDiscord } from "@/components/banniere-discord";
import { EmblemeDeRang } from "@/components/emblemes-de-rang";
import { IconeRole } from "@/components/icones-de-jeu/icone-role";
import { AnneauDeNote } from "@/components/icones-de-jeu/anneau-de-note";
import { IconePiece, TuileDeProfil } from "@/components/tuile-de-profil";
import { SelecteurDePeak, type Peak } from "@/components/selecteur-de-peak";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { costText, costTier } from "@/lib/cost";
import { matchesPlayer, myPlayerQuery } from "@/lib/players";
import { problemMessage } from "@/lib/problem";
import { normalizeTier, rankLabel, roleDeJeu, roleLabel } from "@/lib/ranks";
import { soloQTierClasses } from "@/lib/tier-styles";
import { useTheme } from "@/lib/theme";
import { useDeconnexion } from "@/lib/use-deconnexion";

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

/** Photo, pseudo, bannière et rôle du Compte Discord, avec le rafraîchissement à la demande. */
function CompteDiscord() {
  const { user } = Route.useRouteContext();
  const role = user.role === "Superadministrateur" ? "Superadmin" : user.role === "Administrateur" ? "Admin" : null;

  // Le rafraîchissement passe par l'API puis Discord : il demande une vraie navigation, pas un fetch.
  const rafraichir = () =>
    window.location.assign(`/api/auth/discord/refresh?returnUrl=${encodeURIComponent("/compte")}`);

  return (
    <Card className="gap-0 overflow-hidden py-0">
      <BanniereDiscord url={user.bannerUrl} couleur={user.bannerColor} />
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

/** La vue privée d'un Utilisateur lié : seulement ce que le profil public n'a pas (Joueur lié, résumé, accès). */
function ProfilDuJoueur({ player }: { player: Player }) {
  const fiche = $api.useQuery("get", "/players/{playerId}/fiche", { params: { path: { playerId: player.id } } });

  return (
    <div className="grid items-start gap-4 lg:grid-cols-2">
      <div className="flex min-w-0 flex-col gap-4">
        <JoueurLie player={player} comptes={fiche.data?.comptesDiscord ?? []} />
      </div>
      <div className="flex min-w-0 flex-col gap-4">
        <ResumePublic player={player} noteMoyenne={fiche.data?.stats.noteMoyenne} />
        <AccesRapides />
        <Preferences />
      </div>
    </div>
  );
}

/** Les tuiles de ce que les autres voient (rang, Flex, cost, note, rôle) et le chemin vers le profil public ; sans donnée, pas de tuile. */
function ResumePublic({ player, noteMoyenne }: { player: Player; noteMoyenne: number | null | undefined }) {
  const role = roleDeJeu(player.mainRole);
  const couleurDe = (tier: string | null) => soloQTierClasses[normalizeTier(tier)].split(" ")[0]!;
  const classe = normalizeTier(player.rankTier) !== "UNRANKED";
  const flex = normalizeTier(player.flexRankTier) !== "UNRANKED";

  return (
    <Bloc titre="Ce que les autres voient">
      <div className="grid grid-cols-2 gap-2.5">
        {classe && (
          <TuileDeProfil
            libelle="Classé Solo"
            valeur={rankLabel(player.rankTier, player.rankDivision, player.lp)}
            icone={<EmblemeDeRang tier={player.rankTier} />}
            couleur={couleurDe(player.rankTier)}
          />
        )}
        {flex && (
          <TuileDeProfil
            libelle="Flex"
            valeur={rankLabel(player.flexRankTier, player.flexRankDivision, player.flexLp)}
            icone={<EmblemeDeRang tier={player.flexRankTier} />}
            couleur={couleurDe(player.flexRankTier)}
          />
        )}
        <TuileDeProfil
          libelle="Cost"
          valeur={costText(player.currentCost)}
          detail={`tier ${costTier(player.currentCost)}`}
          icone={<IconePiece />}
          couleur="text-amber-400"
        />
        {noteMoyenne != null && (
          <TuileDeProfil
            libelle="Note moyenne"
            detail="sur 100"
            icone={<AnneauDeNote note={noteMoyenne} />}
            couleur="text-foreground"
          />
        )}
        {role && (
          <TuileDeProfil
            libelle="Rôle principal"
            valeur={roleLabel(player.mainRole) ?? ABSENT}
            icone={<IconeRole role={role} className="size-8 text-sky-400" />}
            couleur="text-sky-400"
          />
        )}
      </div>
      <Link
        to="/joueurs/$playerId"
        params={{ playerId: String(player.id) }}
        className="inline-flex items-center gap-1 text-sm font-medium underline underline-offset-4"
      >
        Voir mon profil public
        <ArrowRightIcon className="size-4" />
      </Link>
    </Bloc>
  );
}

/** Raccourcis vers l'Administration, pour les Administrateurs et le Superadministrateur seulement. */
function AccesRapides() {
  const { user } = Route.useRouteContext();
  if (user.role !== "Administrateur" && user.role !== "Superadministrateur") return null;

  return (
    <Bloc titre="Accès rapides">
      <ul className="flex flex-col divide-y text-sm">
        <li>
          <Link to="/administration" className="flex items-center justify-between gap-3 py-2 font-medium hover:underline">
            Administration
            <span className="font-normal text-muted-foreground">Games, cost, peak, Clips du Fond</span>
          </Link>
        </li>
        <li>
          <Link
            to="/administration"
            search={{ onglet: "administrateurs" }}
            className="flex items-center justify-between gap-3 py-2 font-medium hover:underline"
          >
            Administrateurs
            <span className="font-normal text-muted-foreground">Nommer ou retirer</span>
          </Link>
        </li>
      </ul>
    </Bloc>
  );
}

/** Le Joueur lié : RiotId, changement et déliage ; rôle principal et peak restent discrets. */
function JoueurLie({ player, comptes }: { player: Player; comptes: Schemas["CompteDiscordLie"][] }) {
  const { user } = Route.useRouteContext();
  const [changement, setChangement] = useState(false);
  const [delier, setDelier] = useState(false);
  const [peakOuvert, setPeakOuvert] = useState(false);
  const [effacerPeak, setEffacerPeak] = useState(false);
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: myPlayerQuery.queryKey });
  const unlink = $api.useMutation("delete", "/players/link", {
    onSuccess: async () => {
      await refresh();
      setDelier(false);
    },
  });
  const peakEfface = $api.useMutation("delete", "/players/peak", {
    onSuccess: async () => {
      await refresh();
      setEffacerPeak(false);
    },
  });
  const peak = $api.useMutation("post", "/players/peak", {
    onSuccess: async () => {
      await refresh();
      setPeakOuvert(false);
    },
  });

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
              Me délier
            </Button>
          </div>
        }
      >
        <p className="font-mono text-sm font-semibold">{player.riotId}</p>
        <ComptesDiscordLies comptes={comptes} moi={user.discordUserId} />
        <AvertissementLiaison />
        <dl className="flex flex-col gap-2 border-t pt-3 text-sm text-muted-foreground">
          <div className="flex items-center justify-between gap-3">
            <dt>Rôle principal</dt>
            <dd className="flex items-center gap-1.5 text-foreground">
              {roleDeJeu(player.mainRole) && <IconeRole role={roleDeJeu(player.mainRole)!} className="size-4 text-sky-400" />}
              {role}
            </dd>
          </div>
          <div className="flex items-center justify-between gap-3">
            <dt>Peak</dt>
            <dd className="flex items-center gap-2 text-foreground">
              {aUnPeak ? (
                <>
                  <EmblemeDeRang tier={player.peakTier} className="size-6" />
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
              {!peakOuvert && aUnPeak && (
                <Button variant="ghost" size="sm" className="text-destructive" onClick={() => setEffacerPeak(true)}>
                  Effacer mon peak
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
      <Dialog open={effacerPeak} onOpenChange={setEffacerPeak}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Effacer mon peak ?</DialogTitle>
            <DialogDescription>Ton peak manuel disparaît. Tu pourras en saisir un autre ensuite.</DialogDescription>
          </DialogHeader>
          {peakEfface.isError && (
            <Alert variant="destructive">
              <AlertDescription>{problemMessage(peakEfface.error)}</AlertDescription>
            </Alert>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEffacerPeak(false)}>
              Annuler
            </Button>
            <Button variant="destructive" disabled={peakEfface.isPending} onClick={() => peakEfface.mutate({})}>
              Effacer
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
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

/** Tous les Comptes Discord liés au Joueur, le principal (celui de la photo et de l'identité affichées) marqué. */
function ComptesDiscordLies({ comptes, moi }: { comptes: Schemas["CompteDiscordLie"][]; moi: string | null | undefined }) {
  if (comptes.length === 0) return null;

  return (
    <div className="flex flex-col gap-1.5 border-t pt-3 text-sm">
      <p className="text-muted-foreground">Comptes Discord liés à ce joueur</p>
      <ul aria-label="Comptes Discord liés" className="flex flex-col gap-1.5">
        {comptes.map((c) => (
          <li key={c.discordId} className="flex flex-wrap items-center gap-2">
            <Avatar className="size-6">
              {c.avatarUrl && <AvatarImage src={c.avatarUrl} alt="" />}
              <AvatarFallback className="text-xs">{c.username?.charAt(0).toUpperCase()}</AvatarFallback>
            </Avatar>
            <span className="font-medium">{c.username ?? c.discordId}</span>
            {c.principal && <Badge variant="secondary">Principal</Badge>}
            {c.discordId === moi && <span className="text-muted-foreground">(toi)</span>}
          </li>
        ))}
      </ul>
    </div>
  );
}

/** Le lien est libre : personne ne vérifie qui se lie à qui (ADR 0018). */
function AvertissementLiaison() {
  return (
    <p className="text-xs text-muted-foreground">
      Le lien n'est pas vérifié : n'importe quel compte Discord peut se lier à n'importe quel joueur. Le premier Discord
      lié donne la photo et l'identité affichées.
    </p>
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
        {joueurLieId === undefined && <AvertissementLiaison />}
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
