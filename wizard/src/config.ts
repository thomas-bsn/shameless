// Configuration d'une Instance : validation de la saisie et calcul des variables d'environnement.
// Chaque section (adresse, Discord, et plus tard Riot, Uchuu, fuseau : #71) valide ses champs
// et produit ses variables ; ajouter une section = ajouter une entrée à `SECTIONS`.

export type ConfigInput = Record<string, unknown>;
export type Errors = Record<string, string>;
export type InstanceEnv = Record<string, string>;

export interface Config {
  publicUrl: string;
  clientId: string;
  clientSecret: string;
  guildId: string;
  adminIds: string;
}

/** Secrets internes générés par le wizard (jamais saisis). */
export interface Generated {
  postgresPassword: string;
}

interface Section {
  validate(input: ConfigInput, value: Partial<Config>, errors: Errors): void;
  toEnv(value: Config, generated: Generated): InstanceEnv;
}

const SNOWFLAKE = /^\d{17,20}$/;
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
      // Un domaine : Caddy obtient son certificat HTTPS. Accès local : HTTP sur le port 80 du conteneur.
      SHAMELESS_SITE_ADDRESS: r.https ? r.hostname : ":80",
    };
  },
};

const discord: Section = {
  validate(input, value, errors) {
    const clientId = text(input.clientId);
    if (!SNOWFLAKE.test(clientId)) errors.clientId = "Le Client ID est un nombre de 17 à 20 chiffres (portail développeur Discord, page OAuth2).";
    else value.clientId = clientId;

    const clientSecret = text(input.clientSecret);
    if (clientSecret.length < 16 || /\s/.test(clientSecret))
      errors.clientSecret = "Le Client Secret semble invalide : au moins 16 caractères, sans espace. Générez-le avec « Reset Secret » dans la page OAuth2.";
    else value.clientSecret = clientSecret;

    const guildId = text(input.guildId);
    if (!SNOWFLAKE.test(guildId)) errors.guildId = "L'identifiant du serveur est un nombre de 17 à 20 chiffres (clic droit sur le serveur, « Copier l'identifiant »).";
    else value.guildId = guildId;

    const admins = [...new Set(text(input.adminIds).split(/[\s,;]+/).filter(Boolean))];
    if (admins.length === 0) errors.adminIds = "Indiquez au moins un administrateur : son identifiant Discord (17 à 20 chiffres).";
    else if (!admins.every((a) => SNOWFLAKE.test(a))) errors.adminIds = "Chaque identifiant d'administrateur est un nombre de 17 à 20 chiffres, séparés par une virgule ou un espace.";
    else value.adminIds = admins.join(",");
  },
  toEnv(value) {
    return {
      Auth__Discord__ClientId: value.clientId,
      Auth__Discord__ClientSecret: value.clientSecret,
      Auth__Discord__AllowedGuildId: value.guildId,
      Auth__AdminDiscordIds: value.adminIds,
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

const SECTIONS: Section[] = [adresse, discord, interne];

export function validateConfig(input: ConfigInput): { errors: Errors; value?: Config } {
  const errors: Errors = {};
  const value: Partial<Config> = {};
  for (const s of SECTIONS) s.validate(input, value, errors);
  return Object.keys(errors).length > 0 ? { errors } : { errors, value: value as Config };
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
