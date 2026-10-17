/**
 * Récapitulatif de l'intégration Uchuu (spec #72, ADR 0016) : quelles variables, pour quel composant,
 * avec quelle valeur. Les valeurs rendues contiennent des secrets : ne jamais les journaliser.
 * « Uchuu » désigne le bot Discord de l'Instance (dépôt `thomas-bsn/harceleur_bot`).
 */
import type { SecretsUchuu } from "./secrets.ts";
import { estIdDiscord, FORMAT_ID_DISCORD } from "./validation.ts";

export type Composant = "Shameless" | "Uchuu";

export interface VariableUchuu {
  /** Nom exact de la variable d'environnement chez `composant` (noms Compose côté Shameless). */
  nom: string;
  composant: Composant;
  role: string;
  valeur: string;
}

export interface VariableBotASaisir {
  nom: string;
  facultatif: boolean;
  role: string;
}

export interface EntreeRecapUchuu {
  /** Adresse publique du bot, déjà validée (`validerAdresseBot`). */
  adresseBot: string;
  /** Adresse publique de Shameless, saisie plus tôt dans le wizard. */
  adressePublique: string;
  /** ID du serveur Discord saisi à l'étape Discord (`DISCORD_GUILD_ID`). */
  guildId: string;
  secrets: SecretsUchuu;
}

export interface RecapUchuu {
  variables: VariableUchuu[];
  /** Bloc `.env` à copier dans la configuration d'Uchuu. */
  envUchuu: string;
  /** Variables du bot que l'Opérateur renseigne à la main (le wizard ne les connaît pas). */
  aSaisirAMain: VariableBotASaisir[];
  /** Où trouver la liste complète des variables du bot. */
  docBot: string;
  /** Commande `curl` à lancer soi-même pour vérifier la liaison ; le wizard n'appelle jamais le bot. */
  verification: string;
}

const ROLES = {
  botUrl: "Adresse publique du bot : l'API y envoie les webhooks et lit les candidats vocaux.",
  webUrl: "Adresse publique de Shameless, utilisée pour les liens envoyés au bot.",
  botApiKey: "Clé envoyée par l'API au bot (en-tête Bearer) ; même valeur des deux côtés.",
  webhookSecret: "Secret de signature HMAC-SHA256 des webhooks ; même valeur des deux côtés.",
  botToApiKey: "Clé que le bot présente à l'API sur les routes /bot/… (Bearer) ; même valeur des deux côtés.",
  guildId: "ID du serveur Discord de l'Instance, identique à DISCORD_GUILD_ID côté Shameless.",
  apiUrl:
    "À confirmer : adresse de l'API de Shameless que le bot appelle. Le bot y ajoute /bot/… et l'API est servie sous /api par le proxy ; si le bot attend l'adresse sans /api (son exemple donne https://shameless.example), retirez ce suffixe.",
} as const;

export const VARIABLES_BOT_A_SAISIR: VariableBotASaisir[] = [
  { nom: "SHAMELESS_TEAM1_VOICE_ID", facultatif: false, role: "ID du salon vocal de l'équipe 1." },
  { nom: "SHAMELESS_TEAM2_VOICE_ID", facultatif: false, role: "ID du salon vocal de l'équipe 2." },
  { nom: "SHAMELESS_ANNOUNCE_CHANNEL_ID", facultatif: false, role: "ID du salon textuel des annonces (équipes, games, récaps)." },
  { nom: "SHAMELESS_VOTE_CHANNEL_ID", facultatif: false, role: "ID du salon textuel des votes." },
  { nom: "SHAMELESS_PRONOSTICS_CHANNEL_ID", facultatif: true, role: "ID du salon textuel des pronostics ; sans lui, pas de pronostics." },
  { nom: "SHAMELESS_CLIPS_CHANNEL_ID", facultatif: true, role: "ID du salon textuel des clips ; sans lui, pas de dépôt de clips." },
  { nom: "SHAMELESS_CLIPS_MAX_BYTES", facultatif: true, role: "Taille maximale d'un clip en octets : à aligner sur la même variable côté Shameless (500 Mo par défaut)." },
];

/** Noms des variables du contrat, par composant : figés par un test (ADR 0016). */
export const NOMS_VARIABLES_UCHUU = {
  shameless: ["SHAMELESS_BOT_URL", "SHAMELESS_WEB_URL", "SHAMELESS_BOT_API_KEY", "SHAMELESS_WEBHOOK_SECRET", "SHAMELESS_BOT_TO_API_KEY"],
  bot: ["SHAMELESS_BOT_API_KEY", "SHAMELESS_WEBHOOK_SECRET", "SHAMELESS_BOT_TO_API_KEY", "SHAMELESS_WEB_URL", "SHAMELESS_GUILD_ID", "SHAMELESS_API_URL"],
};

const DOC_BOT = "Les autres variables du bot (jeton, intents, INTERNAL_API_KEY…) sont décrites dans docs/shameless.md du dépôt thomas-bsn/harceleur_bot.";

export function construireRecapUchuu(entree: EntreeRecapUchuu): RecapUchuu {
  if (!estIdDiscord(entree.guildId)) {
    throw new Error(`L'ID du serveur Discord doit être ${FORMAT_ID_DISCORD}.`);
  }
  const webUrl = entree.adressePublique.trim().replace(/\/+$/, "");
  const { botApiKey, webhookSecret, botToApiKey } = entree.secrets;
  const s = (nom: string, role: string, valeur: string): VariableUchuu => ({ nom, composant: "Shameless", role, valeur });
  const u = (nom: string, role: string, valeur: string): VariableUchuu => ({ nom, composant: "Uchuu", role, valeur });

  const variables: VariableUchuu[] = [
    s("SHAMELESS_BOT_URL", ROLES.botUrl, entree.adresseBot),
    s("SHAMELESS_WEB_URL", ROLES.webUrl, webUrl),
    s("SHAMELESS_BOT_API_KEY", ROLES.botApiKey, botApiKey),
    s("SHAMELESS_WEBHOOK_SECRET", ROLES.webhookSecret, webhookSecret),
    s("SHAMELESS_BOT_TO_API_KEY", ROLES.botToApiKey, botToApiKey),
    u("SHAMELESS_BOT_API_KEY", ROLES.botApiKey, botApiKey),
    u("SHAMELESS_WEBHOOK_SECRET", ROLES.webhookSecret, webhookSecret),
    u("SHAMELESS_BOT_TO_API_KEY", ROLES.botToApiKey, botToApiKey),
    u("SHAMELESS_WEB_URL", ROLES.webUrl, webUrl),
    u("SHAMELESS_GUILD_ID", ROLES.guildId, entree.guildId),
    u("SHAMELESS_API_URL", ROLES.apiUrl, `${webUrl}/api`),
  ];

  const envUchuu = [
    "# Intégration Shameless — à reporter dans la configuration d'Uchuu, puis redémarrer le bot.",
    ...variables
      .filter((v) => v.composant === "Uchuu")
      .flatMap((v) => (v.nom === "SHAMELESS_API_URL" ? ["# SHAMELESS_API_URL : à confirmer (le bot attend peut-être l'adresse sans /api)."] : []).concat(`${v.nom}=${v.valeur}`)),
    "",
  ].join("\n");

  return {
    variables,
    envUchuu,
    aSaisirAMain: VARIABLES_BOT_A_SAISIR,
    docBot: DOC_BOT,
    verification: `curl -H "Authorization: Bearer ${botApiKey}" ${entree.adresseBot}/internal/shameless/voice-candidates`,
  };
}
