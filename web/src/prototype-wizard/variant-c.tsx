// PROTOTYPE (issue #68) — Variante C « Console » : esthétique terminal, deux panneaux.
// Gauche : questions de l'étape. Droite : le fichier de configuration qui se construit en direct (secrets masqués).
import { Loader2 } from "lucide-react";
import { useState, type ReactNode } from "react";
import { INSTALL_STEPS, STEPS, TIMEZONES, callbackUrl, mask, type Wizard } from "./state";
import { HELP } from "./ui";

const green = "text-emerald-400";

function Q({ id, k, label, help, error, value, onChange, onBlur, secret }: { id: string; k: string; label: string; help: string; error?: string; value: string; onChange: (v: string) => void; onBlur: () => void; secret?: boolean }) {
  const [reveal, setReveal] = useState(false);
  return (
    <div className="space-y-1">
      <label htmlFor={id} className="block text-zinc-300"><span className={green}>?</span> {label} <span className="text-zinc-500">({k})</span></label>
      <p className="text-xs text-zinc-500"># {help}</p>
      <div className="flex items-center gap-2">
        <span className={green}>›</span>
        <input id={id} value={value} type={secret && !reveal ? "password" : "text"} autoComplete="off" spellCheck={false}
          onChange={(e) => onChange(e.target.value)} onBlur={onBlur} aria-invalid={!!error}
          className="min-w-0 flex-1 border-b border-zinc-700 bg-transparent py-1 text-zinc-100 outline-none focus:border-emerald-400 aria-[invalid=true]:border-red-400" />
        {secret && <button type="button" onClick={() => setReveal(!reveal)} className="text-xs text-zinc-500 hover:text-zinc-200">{reveal ? "[masquer]" : "[voir]"}</button>}
      </div>
      {error && <p role="alert" className="text-xs text-red-400">✗ {error}</p>}
    </div>
  );
}

function Btn({ children, onClick, primary, disabled }: { children: ReactNode; onClick: () => void; primary?: boolean; disabled?: boolean }) {
  return <button onClick={onClick} disabled={disabled} className={`rounded border px-3 py-1.5 text-sm disabled:opacity-40 ${primary ? "border-emerald-400 bg-emerald-400 text-zinc-950 hover:bg-emerald-300" : "border-zinc-700 text-zinc-300 hover:border-zinc-500"}`}>{children}</button>;
}

export function VariantC({ w }: { w: Wizard }) {
  const v = w.values;
  const bar = STEPS.map((_, i) => (i <= w.stepIndex ? "■" : "□")).join("");
  const line = (k: string, val: string, active: boolean, secret = false) => (
    <div className={active ? "bg-emerald-400/10" : ""}><span className="text-sky-300">{k}</span><span className="text-zinc-500">=</span><span className={secret ? "text-amber-300" : "text-zinc-100"}>{val}</span></div>
  );
  const onD = w.step === "discord", onO = w.step === "options";

  return (
    <div className="min-h-full bg-zinc-950 pb-24 font-mono text-sm text-zinc-200">
      <div className="mx-auto max-w-6xl px-4 py-5">
        <div className="mb-6 flex flex-wrap items-center gap-x-4 gap-y-1 border-b border-zinc-800 pb-3">
          <img src="/logo.png" alt="" className="size-6 rounded-full" />
          <span className="font-bold">shameless install</span>
          <span className={green} aria-label={`Étape ${w.stepIndex + 1} sur ${STEPS.length}`}>[{bar}] {w.stepIndex + 1}/{STEPS.length} · {STEPS[w.stepIndex].label}</span>
        </div>

        <div className="grid gap-6 lg:grid-cols-2">
          <div className="space-y-6">
            {w.step === "intro" && (
              <div className="space-y-4">
                <pre className={`${green} text-xs leading-tight`}>{`  ___ _  _   _   __  __ ___ _    ___ ___ ___
 / __| || | /_\\ |  \\/  | __| |  | __/ __/ __|
 \\__ \\ __ |/ _ \\| |\\/| | _|| |__| _|\\__ \\__ \\
 |___/_||_/_/ \\_\\_|  |_|___|____|___|___/___/`}</pre>
                <p>Ce wizard prépare ton Instance et la démarre. ~5 minutes.</p>
                <ul className="space-y-1 text-zinc-400"><li>[1] Discord — OAuth, serveur, admin</li><li>[2] Options — Riot, Uchuu, fuseau</li><li>[3] Récapitulatif puis installation</li></ul>
                <p className="text-zinc-500"># Les clés internes sont générées. Les secrets restent masqués et hors des logs.</p>
                <div className="flex gap-2"><Btn primary onClick={w.next}>Commencer ⏎</Btn><Btn onClick={w.fill}>exemple (proto)</Btn></div>
              </div>
            )}

            {onD && (
              <div className="space-y-5">
                <Q id="c-url" k="SHAMELESS_URL" label="Adresse publique ?" help={HELP.url} value={v.url} onChange={(x) => w.set("url", x)} onBlur={() => w.touch("url")} error={w.shown("url", "discord")} />
                <div className="rounded border border-zinc-800 p-3 text-xs"><p className="text-zinc-400">→ Ajoute ce Redirect dans le portail Discord (OAuth2) :</p><p className={`mt-1 break-all ${green}`}>{callbackUrl(v)}</p></div>
                <Q id="c-cid" k="DISCORD_CLIENT_ID" label="Client ID ?" help={HELP.clientId} value={v.clientId} onChange={(x) => w.set("clientId", x)} onBlur={() => w.touch("clientId")} error={w.shown("clientId", "discord")} />
                <Q id="c-sec" k="DISCORD_CLIENT_SECRET" label="Client Secret ?" help={HELP.clientSecret} secret value={v.clientSecret} onChange={(x) => w.set("clientSecret", x)} onBlur={() => w.touch("clientSecret")} error={w.shown("clientSecret", "discord")} />
                <Q id="c-gid" k="DISCORD_GUILD_ID" label="ID du serveur ?" help={HELP.guildId} value={v.guildId} onChange={(x) => w.set("guildId", x)} onBlur={() => w.touch("guildId")} error={w.shown("guildId", "discord")} />
                <Q id="c-adm" k="ADMIN_DISCORD_IDS" label="Ton ID Discord ?" help={HELP.adminId} value={v.adminId} onChange={(x) => w.set("adminId", x)} onBlur={() => w.touch("adminId")} error={w.shown("adminId", "discord")} />
                <Nav w={w} />
              </div>
            )}

            {onO && (
              <div className="space-y-5">
                {([["riot", "RIOT_API_KEY", "Activer l'API Riot ?", HELP.riot], ["uchuu", "SHAMELESS_BOT_*", "Activer le bot Uchuu ?", HELP.uchuu]] as const).map(([key, k, label, help]) => (
                  <div key={key} className="space-y-1"><p><span className={green}>?</span> {label} <span className="text-zinc-500">({k})</span></p><p className="text-xs text-zinc-500"># {help}</p>
                    <div className="flex gap-2">{[true, false].map((b) => <button key={String(b)} onClick={() => w.set(key, b)} className={`rounded border px-3 py-1 ${v[key] === b ? "border-emerald-400 text-emerald-300" : "border-zinc-700 text-zinc-400"}`}>{b ? "oui" : "non"}</button>)}</div></div>
                ))}
                {v.riot && <Q id="c-riot" k="RIOT_API_KEY" label="Clé Riot ?" help="Commence par RGAPI-" secret value={v.riotKey} onChange={(x) => w.set("riotKey", x)} onBlur={() => w.touch("riotKey")} error={w.shown("riotKey", "options")} />}
                <div className="space-y-1"><label htmlFor="c-tz"><span className={green}>?</span> Fuseau horaire <span className="text-zinc-500">(SHAMELESS_TIMEZONE)</span></label>
                  <select id="c-tz" value={v.timezone} onChange={(e) => w.set("timezone", e.target.value)} className="block w-full border-b border-zinc-700 bg-zinc-950 py-1">{TIMEZONES.map((t) => <option key={t}>{t}</option>)}</select></div>
                <Nav w={w} />
              </div>
            )}

            {w.step === "recap" && (
              <div className="space-y-4">
                <p className={green}>✓ Revois le fichier à droite, puis lance l'installation.</p>
                {Object.values(w.errors).map((m) => <p key={m} className="text-red-400">✗ {m}</p>)}
                <p className="text-zinc-500"># Sauvegarde la base Postgres et les volumes de données.</p>
                <label className="flex items-center gap-2 text-xs text-zinc-500"><input type="checkbox" checked={w.simulateFailure} onChange={(e) => w.setSimulateFailure(e.target.checked)} /> (proto) simuler un échec</label>
                <div className="flex gap-2"><Btn onClick={w.back}>← retour</Btn><Btn primary disabled={Object.keys(w.errors).length > 0} onClick={w.install}>Installer ⏎</Btn></div>
              </div>
            )}

            {w.step === "fin" && (
              <div className="space-y-3">
                {INSTALL_STEPS.map((s, i) => (
                  <div key={s} className={i < w.progress ? green : w.phase === "error" && i === w.progress ? "text-red-400" : "text-zinc-600"}>
                    {i < w.progress ? "✓" : w.phase === "error" && i === w.progress ? "✗" : i === w.progress && w.phase === "running" ? <Loader2 className="inline size-3 animate-spin" /> : "·"} {s}
                  </div>
                ))}
                {w.phase === "error" && <div role="alert" className="rounded border border-red-400/50 p-3 text-red-300">Discord a répondu <b>invalid_client</b>. Vérifie DISCORD_CLIENT_SECRET.<div className="mt-3 flex gap-2"><Btn onClick={() => w.go("discord")}>← corriger</Btn><Btn primary onClick={w.retry}>Réessayer</Btn></div></div>}
                {w.phase === "ok" && <div className={`rounded border border-emerald-400/50 p-3 ${green}`}>✓ Shameless répond sur {v.url}<div className="mt-3"><Btn primary onClick={() => undefined}>Ouvrir le site ⏎</Btn></div></div>}
              </div>
            )}
          </div>

          <aside aria-label="Aperçu de la configuration" className="h-fit rounded border border-zinc-800 bg-zinc-900/60 p-4 text-xs leading-6 lg:sticky lg:top-4">
            <div className="mb-2 text-zinc-500"># .env — généré, jamais édité à la main</div>
            {line("SHAMELESS_URL", v.url || "…", onD)}
            {line("DISCORD_REDIRECT_URI", callbackUrl(v), onD)}
            {line("DISCORD_CLIENT_ID", v.clientId || "…", onD)}
            {line("DISCORD_CLIENT_SECRET", mask(v.clientSecret), onD, true)}
            {line("DISCORD_GUILD_ID", v.guildId || "…", onD)}
            {line("ADMIN_DISCORD_IDS", v.adminId || "…", onD)}
            {line("RIOT_API_KEY", v.riot ? mask(v.riotKey) : "(ignoré)", onO, true)}
            {line("SHAMELESS_BOT_*", v.uchuu ? "(activé)" : "(ignoré)", onO)}
            {line("SHAMELESS_TIMEZONE", v.timezone, onO)}
            <div className="mt-1 text-zinc-500"># POSTGRES_PASSWORD, clés API internes… → générés (••••)</div>
          </aside>
        </div>
      </div>
    </div>
  );
}

function Nav({ w }: { w: Wizard }) {
  return <div className="flex gap-2"><Btn onClick={w.back}>← retour</Btn><Btn primary onClick={w.next}>continuer ⏎</Btn></div>;
}
