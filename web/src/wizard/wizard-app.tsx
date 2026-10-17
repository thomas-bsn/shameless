import { AlertTriangle, Check, Loader2, PartyPopper } from "lucide-react";
import logo from "../../public/logo.png";
import { useCallback, useEffect, useRef, useState } from "react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { cn } from "@/lib/utils";
import { getState, install, unlock, validate, type FieldErrors, type Phase, type RecapUchuu, type Values } from "./api";
import { BoiteACopier, Champ } from "./champs";

const ETAPES = [
  { cle: "intro", libelle: "Introduction" },
  { cle: "adresse", libelle: "Adresse" },
  { cle: "discord", libelle: "Discord" },
  { cle: "options", libelle: "Options" },
  { cle: "recap", libelle: "Récapitulatif" },
  { cle: "fin", libelle: "Installation" },
] as const;
type Etape = (typeof ETAPES)[number]["cle"];

const CHAMPS_PAR_ETAPE: Record<Etape, (keyof Values)[]> = {
  intro: [],
  adresse: ["publicUrl"],
  discord: ["clientId", "clientSecret", "guildId", "adminIds"],
  options: ["riotKey", "timezone", "botUrl"],
  recap: [],
  fin: [],
};

const VIDE: Values = { publicUrl: "", clientId: "", clientSecret: "", guildId: "", adminIds: "", riotKey: "", timezone: "Europe/Paris", uchuu: false, botUrl: "" };
const COMMANDE_RELECTURE = "docker compose exec api cat /instance/instance.env";
const DONNEES_A_SAUVEGARDER =
  "Pensez à configurer les sauvegardes de vos données persistantes dans votre infrastructure : la base Postgres (volume postgres-data), les clips (api-clips), les clés d'authentification (api-keys) et la configuration de l'Instance (instance-config, qui contient des secrets).";

const masquer = (s: string) => (s ? "•".repeat(Math.min(s.length, 24)) : "—");

export function WizardApp({ intervalleMs = 1500 }: { intervalleMs?: number }) {
  const [phase, setPhase] = useState<Phase | "chargement">("chargement");
  const [codeExpire, setCodeExpire] = useState(false);
  const [etape, setEtape] = useState<Etape>("intro");
  const [valeurs, setValeurs] = useState<Values>(VIDE);
  const [touches, setTouches] = useState<Set<keyof Values>>(new Set());
  const [erreurs, setErreurs] = useState<FieldErrors>({});
  const [callback, setCallback] = useState<string | null>(null);
  const [echec, setEchec] = useState("");
  const [urlPublique, setUrlPublique] = useState("");
  const [erreurInstall, setErreurInstall] = useState("");
  const [enCours, setEnCours] = useState(false);
  const [connexionCoupee, setConnexionCoupee] = useState(false);
  const [avertissements, setAvertissements] = useState<string[]>([]);
  const [recap, setRecap] = useState<RecapUchuu | null>(null);

  const appliquer = useCallback((e: { phase: Phase; publicUrl?: string; failure?: string; codeExpired?: boolean; uchuu?: RecapUchuu }) => {
    setPhase(e.phase);
    if (e.uchuu) setRecap(e.uchuu);
    setCodeExpire(!!e.codeExpired);
    if (e.publicUrl) setUrlPublique(e.publicUrl);
    setEchec(e.failure ?? "");
  }, []);

  useEffect(() => {
    void getState().then((r) => {
      appliquer(r.body);
      if (r.body.phase === "installing" || r.body.phase === "done" || r.body.phase === "failed") setEtape("fin");
    });
  }, [appliquer]);

  // Suivi du démarrage. Quand l'adresse change (HTTPS), la connexion à ce wizard peut se couper : on l'explique.
  const echecsReseau = useRef(0);
  useEffect(() => {
    if (etape !== "fin" || phase !== "installing") return;
    const timer = setInterval(() => {
      getState().then(
        (r) => {
          echecsReseau.current = 0;
          setConnexionCoupee(false);
          appliquer(r.body);
        },
        () => {
          if (++echecsReseau.current >= 4) setConnexionCoupee(true);
        },
      );
    }, intervalleMs);
    return () => clearInterval(timer);
  }, [etape, phase, intervalleMs, appliquer]);

  const verifier = useCallback(async (v: Values) => {
    const r = await validate(v);
    setErreurs(r.body.errors ?? {});
    setCallback(r.body.callbackUrl ?? null);
    setAvertissements(r.body.warnings ?? []);
    return r.body.errors ?? {};
  }, []);

  const modifier = <K extends keyof Values>(champ: K, valeur: Values[K]) => setValeurs((v) => ({ ...v, [champ]: valeur }));
  const quitter = (champ: keyof Values) => {
    setTouches((t) => new Set(t).add(champ));
    void verifier(valeurs);
  };
  const erreur = (champ: keyof Values) => (touches.has(champ) ? erreurs[champ] : undefined);

  const index = ETAPES.findIndex((e) => e.cle === etape);
  const aller = (e: Etape) => setEtape(e);

  const continuer = async () => {
    const errs = await verifier(valeurs);
    const champs = CHAMPS_PAR_ETAPE[etape];
    setTouches((t) => new Set([...t, ...champs]));
    if (champs.some((c) => errs[c])) return;
    setEtape(ETAPES[index + 1].cle);
  };

  const installer = async () => {
    setErreurInstall("");
    setEnCours(true);
    try {
      const r = await install(valeurs);
      if (r.status === 202) {
        setUrlPublique(valeurs.publicUrl);
        setRecap(r.body.uchuu ?? null);
        setPhase("installing");
        setEtape("fin");
      } else if (r.status === 422 && r.body.errors) {
        setErreurs(r.body.errors);
        setTouches(new Set(Object.keys(r.body.errors) as (keyof Values)[]));
        setErreurInstall(Object.values(r.body.errors).join(" "));
      } else {
        setErreurInstall(r.body.message ?? "L'installation n'a pas pu démarrer. Réessayez.");
      }
    } catch {
      setErreurInstall("Le wizard est injoignable. Vérifiez que le conteneur tourne (docker compose ps) puis réessayez.");
    } finally {
      setEnCours(false);
    }
  };

  const corriger = () => {
    const premier = (Object.keys(erreurs) as (keyof Values)[])[0];
    setEtape(CHAMPS_PAR_ETAPE.adresse.includes(premier) ? "adresse" : CHAMPS_PAR_ETAPE.options.includes(premier) ? "options" : "discord");
  };

  if (phase === "chargement") return <Cadre><Loader2 className="mx-auto size-6 animate-spin" aria-label="Chargement" /></Cadre>;

  if (phase === "locked") return <Cadre><Verrou expire={codeExpire} onOuvert={(e) => appliquer(e)} /></Cadre>;

  return (
    <Cadre>
      <ol className="flex items-center" aria-label="Progression">
        {ETAPES.map((e, i) => {
          const faite = i < index || phase === "done";
          const courante = i === index;
          return (
            <li key={e.cle} aria-current={courante ? "step" : undefined} className="flex flex-1 items-center last:flex-none">
              <div className="flex flex-col items-center gap-1">
                <span className={cn("flex size-7 items-center justify-center rounded-full border text-xs font-medium",
                  faite && "border-primary bg-primary text-primary-foreground", courante && "border-primary ring-4 ring-primary/20")}>
                  {faite ? <Check className="size-4" /> : i + 1}
                </span>
                <span className={cn("hidden text-[11px] sm:block", courante ? "font-medium" : "text-muted-foreground")}>{e.libelle}</span>
              </div>
              {i < ETAPES.length - 1 && <span className={cn("mx-2 h-px flex-1", faite ? "bg-primary" : "bg-border")} />}
            </li>
          );
        })}
      </ol>
      <p className="text-center text-xs text-muted-foreground sm:hidden">Étape {index + 1} sur {ETAPES.length} : {ETAPES[index].libelle}</p>

      {etape === "intro" && (
        <Card>
          <CardHeader>
            <CardTitle role="heading" aria-level={2} className="text-xl">Bienvenue</CardTitle>
            <CardDescription>Configurons votre Instance Shameless en quelques minutes, sans éditer de fichier .env.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <p className="font-medium">À préparer avant de commencer :</p>
            <ul className="list-disc space-y-1 pl-5 text-muted-foreground">
              <li>L'adresse publique (HTTPS) de votre Instance</li>
              <li>Une application OAuth Discord dans le portail développeur : nous vous guidons</li>
              <li>L'identifiant de votre serveur Discord et le vôtre</li>
            </ul>
            <p className="text-xs text-muted-foreground">Les clés techniques internes sont générées automatiquement.</p>
          </CardContent>
          <CardFooter className="justify-end"><Button onClick={() => aller("adresse")}>Commencer</Button></CardFooter>
        </Card>
      )}

      {etape === "adresse" && (
        <Card>
          <CardHeader>
            <CardTitle role="heading" aria-level={2}>Adresse publique</CardTitle>
            <CardDescription>L'adresse à laquelle vos joueurs ouvriront Shameless.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <Champ label="Adresse publique" placeholder="https://shameless.example.org" value={valeurs.publicUrl}
              onChange={(v) => modifier("publicUrl", v)} onBlur={() => quitter("publicUrl")} error={erreur("publicUrl")}
              aide="Le nom de domaine qui pointe vers ce serveur, en HTTPS, sans chemin. L'URL de callback Discord et les origines autorisées en sont déduites. Le serveur doit être joignable sur les ports 80 et 443 pour obtenir le certificat HTTPS automatiquement." />
          </CardContent>
          <Navigation retour={() => aller("intro")} suite={() => void continuer()} />
        </Card>
      )}

      {etape === "discord" && (
        <Card>
          <CardHeader>
            <CardTitle role="heading" aria-level={2}>Connexion Discord</CardTitle>
            <CardDescription>Vos membres se connecteront avec Discord : il faut une application OAuth.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-5">
            <div className="space-y-2 rounded-lg border border-dashed p-3">
              <p className="text-xs">Dans le portail développeur Discord, onglet OAuth2, section <b>Redirects</b>, ajoutez exactement cette URL :</p>
              <BoiteACopier valeur={callback ?? `${valeurs.publicUrl}/api/auth/discord/callback`} />
            </div>
            <Champ label="Client ID" value={valeurs.clientId} onChange={(v) => modifier("clientId", v)} onBlur={() => quitter("clientId")} error={erreur("clientId")}
              aide="Portail développeur Discord, votre application, OAuth2 : « Client ID »." />
            <Champ label="Client Secret" secret value={valeurs.clientSecret} onChange={(v) => modifier("clientSecret", v)} onBlur={() => quitter("clientSecret")} error={erreur("clientSecret")}
              aide="Même page, bouton « Reset Secret ». Il n'est affiché qu'une fois : copiez-le tout de suite. Il reste masqué ici et n'est jamais écrit dans les journaux." />
            <Champ label="Identifiant du serveur Discord" value={valeurs.guildId} onChange={(v) => modifier("guildId", v)} onBlur={() => quitter("guildId")} error={erreur("guildId")}
              aide="Dans Discord, activez le mode développeur (Paramètres avancés), puis clic droit sur votre serveur, « Copier l'identifiant du serveur ». Seuls ses membres pourront se connecter." />
            <Champ label="Identifiants Discord des administrateurs" value={valeurs.adminIds} onChange={(v) => modifier("adminIds", v)} onBlur={() => quitter("adminIds")} error={erreur("adminIds")}
              aide="Clic droit sur votre profil (mode développeur activé), « Copier l'identifiant de l'utilisateur ». Au moins un ; séparez-en plusieurs par une virgule ou un espace." />
          </CardContent>
          <Navigation retour={() => aller("adresse")} suite={() => void continuer()} />
        </Card>
      )}

      {etape === "options" && (
        <Card>
          <CardHeader>
            <CardTitle role="heading" aria-level={2}>Options</CardTitle>
            <CardDescription>Tout est facultatif : vous pouvez continuer sans rien remplir.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-5">
            <Champ label="Clé de l'API Riot (facultatif)" secret value={valeurs.riotKey} onChange={(v) => modifier("riotKey", v)} onBlur={() => quitter("riotKey")} error={erreur("riotKey")}
              placeholder="RGAPI-…"
              aide="Portail développeur Riot, « Personal API Key » (elle expire et se renouvelle). Sans clé, le rafraîchissement des rangs est désactivé et l'ajout de joueurs depuis Riot échoue : le reste du site fonctionne. Vous pourrez l'ajouter plus tard." />
            <Champ label="Fuseau horaire" value={valeurs.timezone} onChange={(v) => modifier("timezone", v)} onBlur={() => quitter("timezone")} error={erreur("timezone")}
              aide="Nom IANA, par exemple Europe/Paris. Il découpe les jours de votre communauté (un clip est rattaché à la Game lancée le jour de sa publication)." />
            <div className="space-y-3 rounded-lg border p-3">
              <div className="flex items-start justify-between gap-4">
                <div className="space-y-1">
                  <Label htmlFor="uchuu">Intégration Uchuu (bot Discord)</Label>
                  <p className="text-xs text-muted-foreground">Annonces, votes, pronostics et clips dans Discord. Le wizard génère les clés et vous donne les valeurs à copier dans le bot ; il ne contacte ni ne modifie le bot.</p>
                </div>
                <Switch id="uchuu" checked={valeurs.uchuu} onCheckedChange={(v) => { modifier("uchuu", v); if (!v) setErreurs((e) => ({ ...e, botUrl: undefined })); }} />
              </div>
              {valeurs.uchuu && (
                <>
                  <Champ label="Adresse publique du bot" placeholder="https://bot.example.org" value={valeurs.botUrl}
                    onChange={(v) => modifier("botUrl", v)} onBlur={() => quitter("botUrl")} error={erreur("botUrl")}
                    aide="L'adresse à laquelle l'API de Shameless joint le bot (son API interne derrière votre reverse proxy), en HTTPS, sans chemin." />
                  {avertissements.map((a) => <p key={a} className="text-xs text-amber-600 dark:text-amber-400">{a}</p>)}
                </>
              )}
            </div>
          </CardContent>
          <Navigation retour={() => aller("discord")} suite={() => void continuer()} />
        </Card>
      )}

      {etape === "recap" && (
        <Card>
          <CardHeader>
            <CardTitle role="heading" aria-level={2}>Récapitulatif</CardTitle>
            <CardDescription>Vérifiez avant d'installer.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {erreurInstall && (
              <Alert variant="destructive">
                <AlertTriangle />
                <AlertTitle>L'installation n'a pas démarré</AlertTitle>
                <AlertDescription>
                  <p>{erreurInstall}</p>
                  {Object.keys(erreurs).length > 0 && <Button variant="link" className="h-auto p-0" onClick={corriger}>Corriger</Button>}
                </AlertDescription>
              </Alert>
            )}
            <dl className="divide-y text-sm">
              {[
                ["Adresse publique", valeurs.publicUrl],
                ["Callback Discord", callback ?? "—"],
                ["Client ID", valeurs.clientId],
                ["Client Secret", masquer(valeurs.clientSecret)],
                ["Serveur Discord", valeurs.guildId],
                ["Administrateurs", valeurs.adminIds],
                ["Clé Riot", valeurs.riotKey ? masquer(valeurs.riotKey) : "Non renseignée (facultatif)"],
                ["Fuseau horaire", valeurs.timezone],
                ["Intégration Uchuu", valeurs.uchuu ? `Activé : ${valeurs.botUrl}` : "Ignoré"],
                ["Clés internes", valeurs.uchuu ? "Générées automatiquement (base, bot)" : "Générées automatiquement"],
              ].map(([k, v]) => (
                <div key={k} className="flex justify-between gap-4 py-2">
                  <dt className="text-muted-foreground">{k}</dt>
                  <dd className="break-all text-right font-mono text-xs">{v}</dd>
                </div>
              ))}
            </dl>
            {avertissements.map((a) => <p key={a} className="text-xs text-amber-600 dark:text-amber-400">{a}</p>)}
            <p className="text-xs text-muted-foreground">{DONNEES_A_SAUVEGARDER}</p>
          </CardContent>
          <CardFooter className="justify-between">
            <Button variant="ghost" onClick={() => aller("options")}>Retour</Button>
            <Button disabled={enCours} onClick={() => void installer()}>Installer</Button>
          </CardFooter>
        </Card>
      )}

      {etape === "fin" && (
        <Card>
          <CardHeader>
            <CardTitle role="heading" aria-level={2}>{phase === "done" ? "Shameless est prêt" : phase === "failed" ? "L'installation a échoué" : "Installation en cours…"}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            {phase === "installing" && (
              <>
                <p className="flex items-center gap-2"><Loader2 className="size-4 animate-spin" /> Démarrage de la base de données, migrations, puis de l'API. Cela prend en général moins d'une minute.</p>
                {recap && <p className="text-xs text-muted-foreground">En attendant, copiez les valeurs Uchuu ci-dessous : elles ne seront plus affichées quand ce wizard s'arrêtera.</p>}
                {connexionCoupee && (
                  <p className="text-muted-foreground">
                    La connexion à cette page s'est coupée : c'est normal quand le serveur bascule sur votre adresse définitive. Ouvrez{" "}
                    <a className="underline" href={urlPublique}>{urlPublique}</a> dans une minute.
                  </p>
                )}
              </>
            )}
            {phase === "failed" && (
              <Alert variant="destructive">
                <AlertTriangle />
                <AlertTitle>Impossible de démarrer l'Instance</AlertTitle>
                <AlertDescription>
                  <p>{echec}</p>
                  <p>
                    Cliquez sur « Réessayer », corrigez la saisie puis installez à nouveau : la configuration corrigée est relue automatiquement par la base,
                    les migrations, l'API et le proxy, sans commande à lancer. Si l'installation reste bloquée malgré tout, exécutez{" "}
                    <code>docker compose restart migrate api proxy</code> sur votre serveur.
                  </p>
                </AlertDescription>
              </Alert>
            )}
            {phase === "done" && (
              <>
                <p className="flex items-center gap-2 rounded-md bg-emerald-500/10 p-3"><PartyPopper className="size-4" /> Votre Instance répond à {urlPublique}. Ce wizard s'arrête et ne se rouvrira plus.</p>
                <p className="text-xs text-muted-foreground">{DONNEES_A_SAUVEGARDER}</p>
              </>
            )}
          </CardContent>
          {recap && phase !== "failed" && <CardContent className="space-y-5 border-t pt-5"><RecapUchuuEcran recap={recap} /></CardContent>}
          <CardFooter className="justify-end gap-2">
            {phase === "failed" && <Button onClick={() => aller("recap")}>Réessayer</Button>}
            {phase === "done" && <Button asChild><a href={urlPublique}>Ouvrir Shameless</a></Button>}
          </CardFooter>
        </Card>
      )}
    </Cadre>
  );
}

function RecapUchuuEcran({ recap }: { recap: RecapUchuu }) {
  const section = (titre: string, composant: "Shameless" | "Uchuu", aide: string, pied?: React.ReactNode) => (
    <section aria-label={titre} className="space-y-2">
      <h3 className="font-medium">{titre}</h3>
      <p className="text-xs text-muted-foreground">{aide}</p>
      <ul className="divide-y rounded-lg border">
        {recap.variables.filter((v) => v.composant === composant).map((v) => (
          <li key={v.nom} className="space-y-1 p-3">
            <div className="flex items-center gap-2">
              <code className="text-xs font-semibold">{v.nom}</code>
              <span className="rounded bg-muted px-1.5 py-0.5 text-[10px] text-muted-foreground">{composant === "Uchuu" ? "Bot (Uchuu)" : "Shameless (API)"}</span>
            </div>
            <p className="text-xs text-muted-foreground">{v.role}</p>
            <BoiteACopier valeur={v.valeur} libelle={`Copier ${v.nom}`} />
          </li>
        ))}
      </ul>
      {pied}
    </section>
  );
  return (
    <div className="space-y-6 text-sm">
      {section("Dans Shameless", "Shameless", "Déjà enregistré dans la configuration de l'Instance : rien à faire, ces valeurs sont là pour référence.")}
      {section("À reporter dans le bot", "Uchuu", "À coller dans la configuration du bot (fichier .env), puis redémarrer le bot. Les clés sont identiques des deux côtés.",
        <div className="space-y-1"><p className="text-xs text-muted-foreground">Ou tout le bloc d'un coup :</p><BoiteACopier valeur={recap.envUchuu} libelle="Copier le bloc .env du bot" /></div>)}
      <section aria-label="À saisir à la main dans le bot" className="space-y-2">
        <h3 className="font-medium">À saisir à la main dans le bot</h3>
        <p className="text-xs text-muted-foreground">Ces identifiants de salons Discord ne sont pas connus du wizard. {recap.docBot}</p>
        <ul className="space-y-1 text-xs">
          {recap.aSaisirAMain.map((v) => (
            <li key={v.nom}><code className="font-semibold">{v.nom}</code>{v.facultatif ? " (facultatif)" : ""} : {v.role}</li>
          ))}
        </ul>
      </section>
      <section aria-label="Vérifier la liaison" className="space-y-2">
        <h3 className="font-medium">Vérifier la liaison</h3>
        <p className="text-xs text-muted-foreground">Une fois le bot redémarré, cette commande doit répondre par la liste des candidats vocaux (le wizard n'appelle jamais le bot).</p>
        <BoiteACopier valeur={recap.verification} libelle="Copier la commande de vérification" />
      </section>
      <section aria-label="Relire ces valeurs" className="space-y-2">
        <h3 className="font-medium">Relire ces valeurs plus tard</h3>
        <p className="text-xs text-muted-foreground">Quand ce wizard sera arrêté, la configuration reste lisible depuis votre serveur :</p>
        <BoiteACopier valeur={COMMANDE_RELECTURE} libelle="Copier la commande de relecture" />
      </section>
    </div>
  );
}

function Cadre({ children }: { children: React.ReactNode }) {
  return (
    <div className="mx-auto flex min-h-full max-w-xl flex-col gap-6 px-4 py-8">
      <div className="flex items-center gap-3">
        <img src={logo} alt="" className="size-10 rounded-full object-cover" />
        <h1 className="font-bold">Installation de Shameless</h1>
      </div>
      {children}
    </div>
  );
}

function Navigation({ retour, suite }: { retour: () => void; suite: () => void }) {
  return (
    <CardFooter className="justify-between">
      <Button variant="ghost" onClick={retour}>Retour</Button>
      <Button onClick={suite}>Continuer</Button>
    </CardFooter>
  );
}

function Verrou({ expire, onOuvert }: { expire: boolean; onOuvert: (e: { phase: Phase }) => void }) {
  const [code, setCode] = useState("");
  const [message, setMessage] = useState("");
  const ouvrir = async () => {
    const r = await unlock(code);
    if (r.status === 200) onOuvert(r.body as { phase: Phase });
    else setMessage(r.body.message ?? "Code refusé.");
  };
  return (
    <Card>
      <CardHeader>
        <CardTitle role="heading" aria-level={2}>Accès à l'installation</CardTitle>
        <CardDescription>Saisissez le code temporaire affiché dans les journaux du wizard.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3 text-sm">
        <p className="rounded-md bg-muted/50 p-2 font-mono text-xs">docker compose logs wizard</p>
        {expire && (
          <p className="text-destructive">Le code a expiré. Relancez le wizard avec « docker compose restart wizard » pour en obtenir un nouveau.</p>
        )}
        <div className="space-y-1.5">
          <Label htmlFor="code">Code d'accès</Label>
          <Input id="code" autoComplete="off" spellCheck={false} className="font-mono" placeholder="XXXX-XXXX-XXXX-XXXX" value={code}
            onChange={(e) => setCode(e.target.value)} onKeyDown={(e) => e.key === "Enter" && void ouvrir()} />
        </div>
        {message && <Alert variant="destructive"><AlertTriangle /><AlertDescription>{message}</AlertDescription></Alert>}
        <p className="text-xs text-muted-foreground">Le code est à usage unique et valable 30 minutes.</p>
      </CardContent>
      <CardFooter className="justify-end"><Button onClick={() => void ouvrir()}>Déverrouiller</Button></CardFooter>
    </Card>
  );
}
