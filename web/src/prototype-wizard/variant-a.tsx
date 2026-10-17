// PROTOTYPE (issue #68) — Variante A « Pas à pas » : un écran par étape, carte centrée, aide dépliable sous chaque champ.
import { AlertTriangle, Check, ChevronDown, Loader2, PartyPopper } from "lucide-react";
import { useState, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";
import { INSTALL_STEPS, STEPS, TIMEZONES, callbackUrl, mask, type Wizard } from "./state";
import { CopyBox, Field, HELP, Toggle } from "./ui";

function Help({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState(false);
  return (
    <div className="-mt-1">
      <button type="button" onClick={() => setOpen(!open)} className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground">
        Où trouver ça ? <ChevronDown className={cn("size-3 transition-transform", open && "rotate-180")} />
      </button>
      {open && <p className="mt-1 rounded-md bg-muted/50 p-2 text-xs text-muted-foreground">{children}</p>}
    </div>
  );
}

export function VariantA({ w }: { w: Wizard }) {
  const v = w.values;
  return (
    <div className="mx-auto flex min-h-full max-w-xl flex-col gap-6 px-4 py-8 pb-24">
      <div className="flex items-center gap-3">
        <img src="/logo.png" alt="" className="size-10 rounded-full object-cover" />
        <div className="font-bold">Installation de Shameless</div>
      </div>

      <ol className="flex items-center" aria-label="Progression">
        {STEPS.map((s, i) => {
          const done = i < w.stepIndex || w.phase === "ok";
          const current = i === w.stepIndex;
          return (
            <li key={s.key} className="flex flex-1 items-center last:flex-none" aria-current={current ? "step" : undefined}>
              <div className="flex flex-col items-center gap-1">
                <span className={cn("flex size-7 items-center justify-center rounded-full border text-xs font-medium",
                  done && "border-primary bg-primary text-primary-foreground", current && "border-primary ring-4 ring-primary/20")}>
                  {done ? <Check className="size-4" /> : i + 1}
                </span>
                <span className={cn("hidden text-[11px] sm:block", current ? "font-medium" : "text-muted-foreground")}>{s.label}</span>
              </div>
              {i < STEPS.length - 1 && <span className={cn("mx-2 h-px flex-1", done ? "bg-primary" : "bg-border")} />}
            </li>
          );
        })}
      </ol>
      <p className="text-center text-xs text-muted-foreground sm:hidden">Étape {w.stepIndex + 1} sur {STEPS.length} — {STEPS[w.stepIndex].label}</p>

      {w.step === "intro" && (
        <Card>
          <CardHeader>
            <CardTitle className="text-xl">Bienvenue, Opérateur 👋</CardTitle>
            <CardDescription>On configure ton Instance en 4 étapes, environ 5 minutes. Plus de fichier .env à éditer.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <p className="font-medium">Prépare avant de commencer :</p>
            <ul className="space-y-2 text-muted-foreground">
              <li>✔ L'adresse publique de ton Instance</li>
              <li>✔ Une application Discord (portail développeur) — on t'explique comment</li>
              <li>✔ Ton identifiant Discord et celui de ton serveur</li>
              <li>◦ Facultatif : une clé Riot, le bot Uchuu</li>
            </ul>
            <p className="text-xs text-muted-foreground">Les clés techniques internes sont générées pour toi.</p>
          </CardContent>
          <CardFooter className="justify-between">
            <Button variant="ghost" size="sm" onClick={() => { w.fill(); }}>Remplir un exemple (proto)</Button>
            <Button onClick={w.next}>Commencer</Button>
          </CardFooter>
        </Card>
      )}

      {w.step === "discord" && (
        <Card>
          <CardHeader>
            <CardTitle>Connexion Discord</CardTitle>
            <CardDescription>Tes membres se connecteront avec Discord. Il faut une application OAuth.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-5">
            <Field id="a-url" label="Adresse publique" placeholder="https://shameless.example.org" value={v.url}
              onChange={(x) => w.set("url", x)} onBlur={() => w.touch("url")} error={w.shown("url", "discord")} />
            <Help>{HELP.url}</Help>
            <div className="rounded-lg border border-dashed p-3">
              <p className="mb-2 text-xs">Dans le portail Discord → OAuth2 → <b>Redirects</b>, ajoute exactement :</p>
              <CopyBox value={callbackUrl(v)} />
            </div>
            <Field id="a-cid" label="Client ID" value={v.clientId} onChange={(x) => w.set("clientId", x)} onBlur={() => w.touch("clientId")} error={w.shown("clientId", "discord")} />
            <Help>{HELP.clientId}</Help>
            <Field id="a-sec" label="Client Secret" secret value={v.clientSecret} onChange={(x) => w.set("clientSecret", x)} onBlur={() => w.touch("clientSecret")} error={w.shown("clientSecret", "discord")} />
            <Help>{HELP.clientSecret}</Help>
            <Field id="a-gid" label="Identifiant du serveur Discord" value={v.guildId} onChange={(x) => w.set("guildId", x)} onBlur={() => w.touch("guildId")} error={w.shown("guildId", "discord")} />
            <Help>{HELP.guildId}</Help>
            <Field id="a-adm" label="Ton identifiant Discord (administrateur)" value={v.adminId} onChange={(x) => w.set("adminId", x)} onBlur={() => w.touch("adminId")} error={w.shown("adminId", "discord")} />
            <Help>{HELP.adminId}</Help>
          </CardContent>
          <Nav w={w} />
        </Card>
      )}

      {w.step === "options" && (
        <Card>
          <CardHeader>
            <CardTitle>Options</CardTitle>
            <CardDescription>Tout est facultatif. Tu pourras ignorer cette étape.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <Toggle checked={v.riot} onChange={(x) => w.set("riot", x)} label="API Riot" desc={HELP.riot} />
            {v.riot && <Field id="a-riot" label="Clé API Riot" secret value={v.riotKey} onChange={(x) => w.set("riotKey", x)} onBlur={() => w.touch("riotKey")} error={w.shown("riotKey", "options")} placeholder="RGAPI-…" />}
            <Toggle checked={v.uchuu} onChange={(x) => w.set("uchuu", x)} label="Bot Uchuu" desc={HELP.uchuu} />
            <div className="space-y-1.5">
              <label htmlFor="a-tz" className="text-sm font-medium">Fuseau horaire</label>
              <select id="a-tz" value={v.timezone} onChange={(e) => w.set("timezone", e.target.value)} className="h-9 w-full rounded-md border bg-transparent px-3 text-sm dark:bg-input/30">
                {TIMEZONES.map((t) => <option key={t}>{t}</option>)}
              </select>
              <p className="text-xs text-muted-foreground">{HELP.timezone}</p>
            </div>
          </CardContent>
          <Nav w={w} />
        </Card>
      )}

      {w.step === "recap" && (
        <Card>
          <CardHeader>
            <CardTitle>Récapitulatif</CardTitle>
            <CardDescription>Vérifie avant d'installer.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {Object.keys(w.errors).length > 0 && (
              <div role="alert" className="flex gap-2 rounded-md border border-destructive/50 p-3 text-sm text-destructive">
                <AlertTriangle className="size-4 shrink-0" /> Il manque des informations : <button className="underline" onClick={() => w.go("discord")}>retourner à Discord</button>
              </div>
            )}
            <dl className="divide-y text-sm">
              {[
                ["Adresse", v.url || "—"], ["Client ID", v.clientId || "—"], ["Client Secret", mask(v.clientSecret)],
                ["Serveur Discord", v.guildId || "—"], ["Administrateur", v.adminId || "—"],
                ["API Riot", v.riot ? "Activée" : "Ignorée"], ["Bot Uchuu", v.uchuu ? "Activé" : "Ignoré"], ["Fuseau", v.timezone],
                ["Clés internes", "Générées automatiquement"],
              ].map(([k, val]) => (
                <div key={k} className="flex justify-between gap-4 py-2"><dt className="text-muted-foreground">{k}</dt><dd className="break-all text-right font-mono text-xs">{val}</dd></div>
              ))}
            </dl>
            <p className="text-xs text-muted-foreground">Pense à sauvegarder la base Postgres et les volumes de données dans ton infrastructure.</p>
            <label className="flex items-center gap-2 text-xs text-muted-foreground"><input type="checkbox" checked={w.simulateFailure} onChange={(e) => w.setSimulateFailure(e.target.checked)} /> (proto) simuler un échec</label>
          </CardContent>
          <CardFooter className="justify-between">
            <Button variant="ghost" onClick={w.back}>Retour</Button>
            <Button disabled={Object.keys(w.errors).length > 0} onClick={w.install}>Installer</Button>
          </CardFooter>
        </Card>
      )}

      {w.step === "fin" && (
        <Card>
          <CardHeader>
            <CardTitle>{w.phase === "ok" ? "Shameless est prêt" : w.phase === "error" ? "L'installation a échoué" : "Installation en cours…"}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <ul className="space-y-2 text-sm">
              {INSTALL_STEPS.map((s, i) => {
                const failed = w.phase === "error" && i === w.progress;
                const done = i < w.progress;
                return (
                  <li key={s} className="flex items-center gap-2">
                    {failed ? <AlertTriangle className="size-4 text-destructive" /> : done ? <Check className="size-4 text-emerald-500" /> : i === w.progress && w.phase === "running" ? <Loader2 className="size-4 animate-spin" /> : <span className="size-4" />}
                    <span className={cn(!done && !failed && "text-muted-foreground")}>{s}</span>
                  </li>
                );
              })}
            </ul>
            {w.phase === "error" && (
              <div role="alert" className="rounded-md border border-destructive/50 p-3 text-sm">
                <p className="font-medium text-destructive">Impossible de démarrer l'API.</p>
                <p className="text-muted-foreground">Discord a refusé le Client Secret (invalid_client). Vérifie-le à l'étape Discord, puis relance.</p>
              </div>
            )}
            {w.phase === "ok" && (
              <div className="flex items-center gap-2 rounded-md bg-emerald-500/10 p-3 text-sm"><PartyPopper className="size-4" /> Ton Instance répond à {v.url}.</div>
            )}
          </CardContent>
          <CardFooter className="justify-end gap-2">
            {w.phase === "error" && <><Button variant="outline" onClick={() => w.go("discord")}>Corriger Discord</Button><Button onClick={w.retry}>Réessayer</Button></>}
            {w.phase === "ok" && <Button asChild><a href="#ouvrir" onClick={(e) => e.preventDefault()}>Ouvrir Shameless</a></Button>}
          </CardFooter>
        </Card>
      )}
    </div>
  );
}

function Nav({ w }: { w: Wizard }) {
  return (
    <CardFooter className="justify-between">
      <Button variant="ghost" onClick={w.back}>Retour</Button>
      <Button onClick={w.next}>Continuer</Button>
    </CardFooter>
  );
}
