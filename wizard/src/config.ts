// Configuration d'une Instance : validation de la saisie et calcul des variables d'environnement.
// Chaque section (adresse, Discord, options Riot / fuseau / Uchuu) valide ses champs
// et produit ses variables ; ajouter une section = ajouter une entrée à `SECTIONS`.
import type { SecretsUchuu } from "./secrets.ts";
import { estIdDiscord, FORMAT_ID_DISCORD, validerAdresseBot, validerCleRiot, validerFuseau } from "./validation.ts";

export type ConfigInput = Record<string, unknown>;
export type Errors = Record<string, string>;
export type InstanceEnv = Record<string, string>;

export interface Config {
  publicUrl: string;
  clientId: string;
  clientSecret: string;
  guildId: string;
  /**
   * Posé par le serveur du wizard à partir de la connexion Discord de l'Opérateur, jamais par la saisie :
   * premier Superadministrateur, importé par l'API au premier démarrage (ADR 0017).
   */
  superadminId?: string;
  /** Facultative : vide = fonctions Riot désactivées. */
  riotKey: string;
  timezone: string;
  uchuu: boolean;
  /** Adresse publique du bot ; vide quand Uchuu est ignoré. */
  botUrl: string;
  /**
   * Posé par le serveur du wizard, jamais par la saisie : la page a été servie en HTTPS par un proxy
   * en amont (Traefik de Forgeyard...) qui joint Caddy en HTTP. Caddy ne gère alors pas le HTTPS.
   */
  tlsEnAmont?: boolean;
}

/** Secrets internes générés par le wizard (jamais saisis). */
export interface Generated {
  postgresPassword: string;
  /** Générés à la première confirmation avec Uchuu activé, puis réutilisés aux relances. */
  uchuu?: SecretsUchuu;
}

interface Section {
  validate(input: ConfigInput, value: Partial<Config>, errors: Errors, warnings: string[]): void;
  toEnv(value: Config, generated: Generated): InstanceEnv;
}

const LOCAL_HOSTS = new Set(["localhost", "127.0.0.1", "[::1]"]);

const text = (v: unknown): string => (typeof v === "string" ? v.trim() : "");

/** Origine (schéma + hôte [+ port]) d'une adresse publique valide, sinon un message d'erreur. */
function parsePublicUrl(raw: string): { origin: string; https: boolean; hostname: string } | string {
  if (!raw) return "Renseignez l'adresse publique de votre Instance, par exemple https://shameless.example.org.";
  let url: URL;
  try {
    url = new URL(raw);
  } catch {
    return "Adresse invalide : elle doit ressembler à https://shameless.example.org.";
  }
  const https = url.protocol === "https:";
  if (!https && !(url.protocol === "http:" && LOCAL_HOSTS.has(url.hostname)))
    return "L'adresse publique doit commencer par https:// (http:// n'est accepté que pour un accès local).";
  if (url.username || url.password || url.search || url.hash || url.pathname !== "/")
    return "L'adresse publique ne doit contenir ni chemin, ni paramètres, ni identifiants : seulement le domaine (et le port si besoin).";
  return { origin: url.origin, https, hostname: url.hostname };
}

const adresse: Section = {
  validate(input, value, errors) {
    const r = parsePublicUrl(text(input.publicUrl));
    if (typeof r === "string") errors.publicUrl = r;
    else value.publicUrl = r.origin;
  },
  toEnv(value) {
    const r = parsePublicUrl(value.publicUrl) as { https: boolean; hostname: string };
    return {
      Auth__Discord__RedirectUri: `${value.publicUrl}/api/auth/discord/callback`,
      Auth__AllowedReturnOrigins: value.publicUrl,
      // Un domaine joint directement : Caddy obtient son certificat HTTPS. Accès local, ou HTTPS terminé
      // par un proxy en amont (qui joint Caddy en HTTP, sans redirection) : HTTP sur le port 80.
      SHAMELESS_SITE_ADDRESS: r.https && !value.tlsEnAmont ? r.hostname : ":80",
    };
  },
};

const discord: Section = {
  validate(input, value, errors) {
    const clientId = text(input.clientId);
    if (!estIdDiscord(clientId)) errors.clientId = `Le Client ID est ${FORMAT_ID_DISCORD} (portail développeur Discord, page OAuth2).`;
    else value.clientId = clientId;

    const clientSecret = text(input.clientSecret);
    if (clientSecret.length < 16 || /\s/.test(clientSecret))
      errors.clientSecret = "Le Client Secret semble invalide : au moins 16 caractères, sans espace. Générez-le avec « Reset Secret » dans la page OAuth2.";
    else value.clientSecret = clientSecret;

    const guildId = text(input.guildId);
    if (!estIdDiscord(guildId)) errors.guildId = `L'identifiant du serveur est ${FORMAT_ID_DISCORD} (clic droit sur le serveur, « Copier l'identifiant »).`;
    else value.guildId = guildId;

  },
  toEnv(value) {
    if (!value.superadminId) throw new Error("Le premier Superadministrateur doit se connecter avec Discord avant l'écriture de la configuration.");
    return {
      Auth__Discord__ClientId: value.clientId,
      Auth__Discord__ClientSecret: value.clientSecret,
      Auth__Discord__AllowedGuildId: value.guildId,
      Auth__AdminDiscordIds: value.superadminId,
    };
  },
};

const interne: Section = {
  validate() {},
  toEnv(_value, generated) {
    return {
      POSTGRES_PASSWORD: generated.postgresPassword,
      ConnectionStrings__Shameless: `Host=postgres;Database=shameless;Username=shameless;Password=${generated.postgresPassword}`,
    };
  },
};

const options: Section = {
  validate(input, value, errors, warnings) {
    const riot = validerCleRiot(text(input.riotKey));
    if (riot.ok) value.riotKey = riot.valeur;
    else errors.riotKey = riot.erreur;

    const fuseau = validerFuseau(text(input.timezone));
    if (fuseau.ok) value.timezone = fuseau.valeur;
    else errors.timezone = fuseau.erreur;

    value.uchuu = input.uchuu === true;
    value.botUrl = "";
    if (value.uchuu) {
      const bot = validerAdresseBot(text(input.botUrl));
      if (bot.ok) {
        value.botUrl = bot.valeur;
        warnings.push(...bot.avertissements);
      } else errors.botUrl = bot.erreur;
    }
  },
  toEnv(value, generated) {
    const env: InstanceEnv = { RIOT_API_KEY: value.riotKey, SHAMELESS_TIMEZONE: value.timezone };
    if (!value.uchuu) return env;
    if (!generated.uchuu) throw new Error("Les secrets Uchuu doivent être générés avant l'écriture de la configuration.");
    return {
      ...env,
      SHAMELESS_BOT_URL: value.botUrl,
      SHAMELESS_WEB_URL: value.publicUrl,
      SHAMELESS_BOT_API_KEY: generated.uchuu.botApiKey,
      SHAMELESS_WEBHOOK_SECRET: generated.uchuu.webhookSecret,
      SHAMELESS_BOT_TO_API_KEY: generated.uchuu.botToApiKey,
    };
  },
};

const SECTIONS: Section[] = [adresse, discord, options, interne];

export function validateConfig(input: ConfigInput): { errors: Errors; warnings: string[]; value?: Config } {
  const errors: Errors = {};
  const warnings: string[] = [];
  const value: Partial<Config> = {};
  for (const s of SECTIONS) s.validate(input, value, errors, warnings);
  return Object.keys(errors).length > 0 ? { errors, warnings } : { errors, warnings, value: value as Config };
}

export function buildInstanceEnv(value: Config, generated: Generated): InstanceEnv {
  return Object.assign({}, ...SECTIONS.map((s) => s.toEnv(value, generated))) as InstanceEnv;
}

/** Format `CLE='valeur'` : sourçable par `sh`, sans interprétation des `$` ni des espaces. */
export function serializeEnv(env: InstanceEnv): string {
  return Object.entries(env)
    .map(([k, v]) => `${k}='${v.replaceAll("'", "'\\''")}'\n`)
    .join("");
}
