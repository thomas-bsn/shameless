// PROTOTYPE (issue #68) — Variante B « Une seule page » : toutes les sections visibles, navigation latérale collante
// avec statut de chaque section, aide dans la marge. Pas de « Suivant » : on remplit dans l'ordre qu'on veut.
import { AlertTriangle, Check, Circle, Loader2 } from "lucide-react";
import type { ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { INSTALL_STEPS, TIMEZONES, callbackUrl, type Wizard } from "./state";
import { CopyBox, Field, HELP, Toggle } from "./ui";

function Section({ id, n, title, status, children, aside }: { id: string; n: number; title: string; status: "ok" | "todo" | "optional"; children: ReactNode; aside?: ReactNode }) {
  return (
    <section id={id} className="scroll-mt-6 grid gap-6 border-t pt-8 lg:grid-cols-[1fr_16rem]">
      <div className="space-y-4">
        <h2 className="flex items-center gap-3 text-lg font-semibold">
          <span className={cn("flex size-7 items-center justify-center rounded-md text-xs", status === "ok" ? "bg-emerald-500 text-white" : "bg-muted")}>{status === "ok" ? <Check className="size-4" /> : n}</span>
          {title}
          {status === "optional" && <span className="rounded bg-muted px-1.5 py-0.5 text-[10px] font-normal uppercase tracking-wide text-muted-foreground">facultatif</span>}
        </h2>
        {children}
      </div>
      {aside && <aside className="space-y-2 text-xs text-muted-foreground lg:border-l lg:pl-4">{aside}</aside>}
    </section>
  );
}

export function VariantB({ w }: { w: Wizard }) {
  const v = w.values;
  const e = w.errors;
  const addrOk = !e.url;
  const discordOk = !e.clientId && !e.clientSecret && !e.guildId && !e.adminId;
  const allOk = addrOk && discordOk && !e.riotKey;
  const sections = [
    { id: "b-adresse", label: "Adresse", ok: addrOk },
    { id: "b-discord", label: "Discord", ok: discordOk },
    { id: "b-options", label: "Options", ok: !e.riotKey, optional: true },
    { id: "b-recap", label: "Installer", ok: allOk },
  ];
  const done = sections.filter((s) => s.ok && !s.optional).length;

  if (w.step === "fin") return <Finish w={w} />;

  return (
    <div className="mx-auto max-w-5xl px-4 pb-32 pt-6">
      {/* Mobile : barre de progression collante */}
      <div className="sticky top-0 z-10 -mx-4 mb-4 border-b bg-background/95 px-4 py-2 backdrop-blur lg:hidden">
        <div className="mb-1 flex justify-between text-xs"><span>{done}/3 sections requises</span><button className="underline" onClick={w.fill}>exemple</button></div>
        <div className="h-1.5 rounded bg-muted"><div className="h-full rounded bg-primary transition-all" style={{ width: `${(done / 3) * 100}%` }} /></div>
      </div>

      <div className="grid gap-10 lg:grid-cols-[13rem_1fr]">
        <nav aria-label="Sections" className="sticky top-6 hidden h-fit space-y-1 lg:block">
          <div className="mb-4 flex items-center gap-2 font-bold"><img src="/logo.png" alt="" className="size-8 rounded-full" /> Shameless</div>
          {sections.map((s) => (
            <a key={s.id} href={`#${s.id}`} className="flex items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-accent">
              {s.ok ? <Check className="size-4 text-emerald-500" /> : <Circle className="size-4 text-muted-foreground" />}
              {s.label}
            </a>
          ))}
          <div className="mt-4 h-1.5 rounded bg-muted"><div className="h-full rounded bg-primary transition-all" style={{ width: `${(done / 3) * 100}%` }} /></div>
          <button className="mt-3 text-xs text-muted-foreground underline" onClick={w.fill}>Remplir un exemple (proto)</button>
        </nav>

        <div className="space-y-10">
          <header className="space-y-2">
            <h1 className="text-3xl font-bold tracking-tight">Installer ton Instance</h1>
            <p className="text-muted-foreground">Une seule page, dans l'ordre que tu veux. Les clés techniques sont générées. Environ 5 minutes.</p>
          </header>

          <Section id="b-adresse" n={1} title="Adresse publique" status={addrOk ? "ok" : "todo"} aside={<p>{HELP.url}</p>}>
            <Field id="b-url" label="Adresse" placeholder="https://shameless.example.org" value={v.url} onChange={(x) => w.set("url", x)} onBlur={() => w.touch("url")} error={w.shown("url", "discord")} />
          </Section>

          <Section id="b-discord" n={2} title="Application Discord" status={discordOk ? "ok" : "todo"} aside={<>
            <p className="font-medium text-foreground">Dans le portail développeur</p>
            <ol className="list-decimal space-y-1 pl-4">
              <li>Crée une application</li><li>OAuth2 → ajoute le Redirect ci-contre</li><li>Copie Client ID et Secret</li>
            </ol>
            <p>{HELP.clientSecret}</p>
          </>}>
            <CopyBox label="Redirect à ajouter dans Discord" value={callbackUrl(v)} />
            <div className="grid gap-4 sm:grid-cols-2">
              <Field id="b-cid" label="Client ID" value={v.clientId} onChange={(x) => w.set("clientId", x)} onBlur={() => w.touch("clientId")} error={w.shown("clientId", "discord")} />
              <Field id="b-sec" label="Client Secret" secret value={v.clientSecret} onChange={(x) => w.set("clientSecret", x)} onBlur={() => w.touch("clientSecret")} error={w.shown("clientSecret", "discord")} />
              <Field id="b-gid" label="ID du serveur Discord" hint={HELP.guildId} value={v.guildId} onChange={(x) => w.set("guildId", x)} onBlur={() => w.touch("guildId")} error={w.shown("guildId", "discord")} />
              <Field id="b-adm" label="Ton ID Discord (admin)" hint={HELP.adminId} value={v.adminId} onChange={(x) => w.set("adminId", x)} onBlur={() => w.touch("adminId")} error={w.shown("adminId", "discord")} />
            </div>
          </Section>

          <Section id="b-options" n={3} title="Options" status="optional" aside={<p>Tu peux tout laisser tel quel : l'installation fonctionne sans Riot ni Uchuu.</p>}>
            <div className="grid gap-3 sm:grid-cols-2">
              <Toggle checked={v.riot} onChange={(x) => w.set("riot", x)} label="API Riot" desc={HELP.riot} />
              <Toggle checked={v.uchuu} onChange={(x) => w.set("uchuu", x)} label="Bot Uchuu" desc={HELP.uchuu} />
            </div>
            {v.riot && <Field id="b-riot" label="Clé API Riot" secret value={v.riotKey} placeholder="RGAPI-…" onChange={(x) => w.set("riotKey", x)} onBlur={() => w.touch("riotKey")} error={w.shown("riotKey", "options")} />}
            <div className="space-y-1.5"><label className="text-sm font-medium" htmlFor="b-tz">Fuseau horaire</label>
              <select id="b-tz" value={v.timezone} onChange={(ev) => w.set("timezone", ev.target.value)} className="h-9 w-full max-w-xs rounded-md border bg-transparent px-3 text-sm dark:bg-input/30">{TIMEZONES.map((t) => <option key={t}>{t}</option>)}</select>
            </div>
          </Section>

          <Section id="b-recap" n={4} title="Installer" status={allOk ? "ok" : "todo"} aside={<p>Pense aux sauvegardes : base Postgres + volumes de données.</p>}>
            {!allOk ? (
              <div role="alert" className="space-y-1 rounded-md border border-destructive/50 p-3 text-sm">
                <p className="flex items-center gap-2 font-medium text-destructive"><AlertTriangle className="size-4" /> Reste à corriger</p>
                <ul className="list-disc pl-6 text-muted-foreground">{Object.entries(e).map(([k, m]) => <li key={k}>{m}</li>)}</ul>
              </div>
            ) : <p className="text-sm text-muted-foreground">Tout est prêt. {v.uchuu ? "Les valeurs pour Uchuu te seront données à la fin." : ""}</p>}
            <label className="flex items-center gap-2 text-xs text-muted-foreground"><input type="checkbox" checked={w.simulateFailure} onChange={(ev) => w.setSimulateFailure(ev.target.checked)} /> (proto) simuler un échec</label>
            <Button size="lg" disabled={!allOk} onClick={w.install}>Installer Shameless</Button>
          </Section>
        </div>
      </div>
    </div>
  );
}

function Finish({ w }: { w: Wizard }) {
  return (
    <div className="mx-auto max-w-2xl px-4 py-16">
      <div className="space-y-6 rounded-xl border p-8">
        <h1 className="text-2xl font-bold">{w.phase === "ok" ? "C'est installé 🎉" : w.phase === "error" ? "Échec de l'installation" : "Installation…"}</h1>
        <div className="h-2 rounded bg-muted"><div className={cn("h-full rounded transition-all", w.phase === "error" ? "bg-destructive" : "bg-primary")} style={{ width: `${(w.progress / INSTALL_STEPS.length) * 100}%` }} /></div>
        <p className="flex items-center gap-2 text-sm text-muted-foreground">
          {w.phase === "running" && <Loader2 className="size-4 animate-spin" />}
          {w.phase === "ok" ? "Terminé." : INSTALL_STEPS[Math.min(w.progress, INSTALL_STEPS.length - 1)]}
        </p>
        {w.phase === "error" && <div role="alert" className="rounded-md border border-destructive/50 p-3 text-sm"><b className="text-destructive">Discord a refusé le Client Secret.</b><br />Corrige-le dans la section Discord puis relance.</div>}
        {w.phase === "ok" && w.values.uchuu && (
          <div className="space-y-2 text-sm"><p className="font-medium">À reporter dans la configuration d'Uchuu :</p><CopyBox value={`SHAMELESS_URL=${w.values.url}`} /><CopyBox value="SHAMELESS_WEBHOOK_SECRET=••• (généré)" /></div>
        )}
        <div className="flex justify-end gap-2">
          {w.phase === "error" && <><Button variant="outline" onClick={() => w.go("discord")}>Modifier la configuration</Button><Button onClick={w.retry}>Réessayer</Button></>}
          {w.phase === "ok" && <Button>Ouvrir {w.values.url}</Button>}
        </div>
      </div>
    </div>
  );
}
