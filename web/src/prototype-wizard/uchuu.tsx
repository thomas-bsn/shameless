// PROTOTYPE (issue #68, contrat #72) — étape « Bot Discord (Uchuu) » et bloc de fin, trois mises en forme. Jetable.
import { AlertTriangle, Check, Copy, ExternalLink, KeyRound, Terminal } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { FAKE_SECRETS, checkBotUrl, type Wizard } from "./state";
import { CopyBox, Field, HELP, Toggle } from "./ui";

export type UchuuVariant = "1" | "2" | "3";

const DOC = "https://github.com/…/Uchuu/blob/main/docs/shameless.md";

/** Variables à reporter dans Uchuu (SHAMELESS_BOT_TO_API_KEY volontairement absente : à confirmer, #72 Q5). */
function rows(w: Wizard) {
  const v = w.values;
  return [
    { name: "SHAMELESS_WEB_URL", where: "Shameless + Uchuu", role: "Adresse publique de Shameless, pour les liens envoyés par le bot.", value: v.url, secret: false },
    { name: "SHAMELESS_BOT_URL", where: "Shameless", role: "Adresse publique du bot : l'API Shameless y envoie ses webhooks.", value: v.botUrl, secret: false },
    { name: "SHAMELESS_BOT_API_KEY", where: "Shameless + Uchuu", role: "Clé envoyée par l'API au bot (Bearer). Même valeur des deux côtés.", value: FAKE_SECRETS.botApiKey, secret: true },
    { name: "SHAMELESS_WEBHOOK_SECRET", where: "Shameless + Uchuu", role: "Signature HMAC des webhooks. Même valeur des deux côtés.", value: FAKE_SECRETS.webhookSecret, secret: true },
    { name: "SHAMELESS_GUILD_ID", where: "Uchuu", role: "Ton serveur Discord (repris de l'étape Discord).", value: v.guildId, secret: false },
  ];
}
const toEnv = (w: Wizard) =>
  rows(w).filter((r) => r.where.includes("Uchuu")).map((r) => `${r.name}=${r.value}`).join("\n");
const MANUAL = ["SHAMELESS_TEAM1_VOICE_ID", "SHAMELESS_TEAM2_VOICE_ID", "SHAMELESS_ANNOUNCE_CHANNEL_ID"];
const verifyCmd = (w: Wizard) => `curl -H "Authorization: Bearer ${FAKE_SECRETS.botApiKey}" \\\n  ${w.values.botUrl}/internal/shameless/voice-candidates`;

function Copy1({ text, label = "Copier" }: { text: string; label?: string }) {
  const [ok, setOk] = useState(false);
  return (
    <Button size="sm" variant="outline" onClick={() => { void navigator.clipboard?.writeText(text); setOk(true); setTimeout(() => setOk(false), 1200); }}>
      {ok ? <Check className="size-4" /> : <Copy className="size-4" />} {ok ? "Copié" : label}
    </Button>
  );
}

/* ------------------------------ Étape Options ------------------------------ */

export function UchuuOptions({ w, variant }: { w: Wizard; variant: UchuuVariant }) {
  const v = w.values;
  const chk = v.uchuu ? checkBotUrl(v.botUrl) : {};
  const error = w.shown("botUrl", "options");
  return (
    <div className="space-y-3">
      <Toggle checked={v.uchuu} onChange={(x) => w.set("uchuu", x)} label="Bot Discord (Uchuu)" desc={HELP.uchuu} />
      {v.uchuu && (
        <div className={cn("space-y-3 rounded-lg border p-3", variant === "2" && "border-l-4 border-l-primary", variant === "3" && "bg-muted/30")}>
          <Field id="a-bot" label="Adresse publique du bot" placeholder="https://bot.example.org" value={v.botUrl}
            onChange={(x) => w.set("botUrl", x)} onBlur={() => w.touch("botUrl")} error={error}
            hint="Ex. https://bot.example.org — le domaine derrière lequel ton bot est exposé en HTTPS, sans chemin." />
          {!error && chk.warning && v.botUrl && (
            <p className="flex gap-2 rounded-md bg-amber-500/10 p-2 text-xs"><AlertTriangle className="size-4 shrink-0 text-amber-500" /> {chk.warning}</p>
          )}
          {variant === "1" && <p className="text-xs text-muted-foreground"><KeyRound className="mr-1 inline size-3" />3 clés seront générées pour toi. Rien d'autre à saisir ici.</p>}
          {variant === "2" && (
            <ul className="space-y-1 text-xs text-muted-foreground">
              {["Clé API Shameless → bot", "Secret des webhooks", "Clé bot → API (liaison à confirmer)"].map((k) => (
                <li key={k} className="flex items-center gap-2"><KeyRound className="size-3" /> {k} <span className="ml-auto rounded bg-muted px-1.5">générée</span></li>
              ))}
            </ul>
          )}
          {variant === "3" && (
            <details className="text-xs text-muted-foreground">
              <summary className="cursor-pointer">Ce que le wizard fait et ne fait pas</summary>
              <p className="mt-1">Il génère les clés et les enregistre côté Shameless. Il n'installe pas Uchuu, n'appelle pas le bot et ne touche pas à ton serveur Discord : tu reportes ensuite les valeurs dans la config du bot.</p>
            </details>
          )}
        </div>
      )}
    </div>
  );
}

/* ---------------------------- Récap (une ligne) ---------------------------- */

export const recapUchuu = (w: Wizard) => (w.values.uchuu ? `Activé — ${w.values.botUrl || "adresse manquante"} · 3 clés générées` : "Ignoré");

/* -------------------------------- Écran de fin ----------------------------- */

const Footer = () => (
  <div className="space-y-1 rounded-md border border-amber-500/40 bg-amber-500/5 p-3 text-xs">
    <p className="font-medium">Cet écran ne sera plus accessible.</p>
    <p className="text-muted-foreground">Les valeurs restent lisibles dans <code>instance.env</code> :</p>
    <code className="block rounded bg-muted px-2 py-1">docker compose exec api cat /data/instance.env</code>
    <p className="text-muted-foreground">Ne les colle pas dans un canal non sûr. Redémarre Uchuu après les avoir reportées ; pour une rotation, change les deux côtés.</p>
  </div>
);

export function UchuuEnd({ w, variant }: { w: Wizard; variant: UchuuVariant }) {
  if (!w.values.uchuu) return null;
  return (
    <div className="space-y-4 border-t pt-4">
      <h3 className="font-semibold">Bot Discord (Uchuu)</h3>
      {variant === "1" && <EndBlock w={w} />}
      {variant === "2" && <EndTable w={w} />}
      {variant === "3" && <EndSteps w={w} />}
      <Footer />
    </div>
  );
}

/** 1 — Un bloc .env copiable + liste de ce qu'il reste à faire. */
function EndBlock({ w }: { w: Wizard }) {
  return (
    <div className="space-y-3 text-sm">
      <p className="text-xs text-muted-foreground">Côté Shameless, c'est déjà enregistré. Reporte ceci dans le <code>.env</code> d'Uchuu :</p>
      <div className="rounded-md border bg-muted/40 p-3">
        <pre className="overflow-x-auto break-all whitespace-pre-wrap text-xs">{toEnv(w)}</pre>
        <div className="mt-2"><Copy1 text={toEnv(w)} label="Copier le bloc" /></div>
      </div>
      <div>
        <p className="font-medium">À renseigner à la main côté Uchuu :</p>
        <ul className="mt-1 list-inside list-disc font-mono text-xs text-muted-foreground">{MANUAL.map((m) => <li key={m}>{m}</li>)}</ul>
        <a href={DOC} onClick={(e) => e.preventDefault()} className="mt-1 inline-flex items-center gap-1 text-xs underline">Doc d'Uchuu <ExternalLink className="size-3" /></a>
      </div>
    </div>
  );
}

/** 2 — Tableau variable par variable : nom, composant, rôle, valeur, copie. */
function EndTable({ w }: { w: Wizard }) {
  return (
    <ul className="divide-y rounded-md border text-sm">
      {rows(w).map((r) => (
        <li key={r.name} className="space-y-1 p-3">
          <div className="flex flex-wrap items-center gap-2">
            <code className="text-xs font-semibold">{r.name}</code>
            <span className={cn("rounded-full px-2 py-0.5 text-[10px]", r.where === "Shameless" ? "bg-muted" : "bg-primary/15")}>{r.where}</span>
          </div>
          <p className="text-xs text-muted-foreground">{r.role}</p>
          <CopyBox value={r.value} />
        </li>
      ))}
      <li className="space-y-1 bg-muted/30 p-3 text-xs">
        <p className="font-medium">Variables propres à ton serveur (côté Uchuu) :</p>
        <p className="font-mono text-muted-foreground">{MANUAL.join(" · ")}</p>
        <a href={DOC} onClick={(e) => e.preventDefault()} className="inline-flex items-center gap-1 underline">Doc d'Uchuu <ExternalLink className="size-3" /></a>
      </li>
    </ul>
  );
}

/** 3 — Trois actions numérotées : copier, compléter, redémarrer et vérifier. */
function EndSteps({ w }: { w: Wizard }) {
  const [tab, setTab] = useState<"uchuu" | "shameless">("uchuu");
  return (
    <ol className="space-y-4 text-sm">
      <li className="flex gap-3">
        <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-primary text-xs text-primary-foreground">1</span>
        <div className="min-w-0 flex-1 space-y-2">
          <p className="font-medium">Copie les valeurs</p>
          <div className="flex gap-1 text-xs">
            {(["uchuu", "shameless"] as const).map((t) => (
              <button key={t} onClick={() => setTab(t)} className={cn("rounded-full border px-3 py-1", tab === t && "bg-accent font-medium")}>
                {t === "uchuu" ? "Dans Uchuu" : "Déjà dans Shameless"}
              </button>
            ))}
          </div>
          <pre className="overflow-x-auto break-all whitespace-pre-wrap rounded-md border bg-muted/40 p-3 text-xs">
            {tab === "uchuu" ? toEnv(w) : rows(w).filter((r) => r.where.startsWith("Shameless")).map((r) => `${r.name}=${r.secret ? "•••• (générée)" : r.value}`).join("\n")}
          </pre>
          {tab === "uchuu" && <Copy1 text={toEnv(w)} label="Copier le .env" />}
        </div>
      </li>
      <li className="flex gap-3">
        <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-primary text-xs text-primary-foreground">2</span>
        <div className="space-y-1">
          <p className="font-medium">Complète les identifiants de ton serveur</p>
          <p className="font-mono text-xs text-muted-foreground">{MANUAL.join(" · ")}</p>
          <a href={DOC} onClick={(e) => e.preventDefault()} className="inline-flex items-center gap-1 text-xs underline">Doc d'Uchuu <ExternalLink className="size-3" /></a>
        </div>
      </li>
      <li className="flex gap-3">
        <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-primary text-xs text-primary-foreground">3</span>
        <div className="min-w-0 flex-1 space-y-2">
          <p className="font-medium">Redémarre Uchuu puis vérifie la liaison</p>
          <pre className="overflow-x-auto whitespace-pre-wrap break-all rounded-md border bg-muted/40 p-3 text-xs"><Terminal className="mr-1 inline size-3" />{verifyCmd(w)}</pre>
          <p className="text-xs text-muted-foreground">Réponse 200 : le bot accepte la clé. Le wizard n'appelle jamais ton bot.</p>
        </div>
      </li>
    </ol>
  );
}
