/**
 * Récapitulatif de l'intégration Uchuu (spec #72) : quelles variables, pour quel composant,
 * avec quelle valeur. N'inclut côté Uchuu que ce qui est confirmé ; `SHAMELESS_BOT_TO_API_KEY`
 * reste dans `aConfirmer` tant que le dépôt Uchuu à jour n'a pas confirmé son nom et son usage
 * (question ouverte n°5). Les valeurs rendues contiennent des secrets : ne jamais les journaliser.
 */
import type { SecretsUchuu } from "@/lib/wizard/secrets";

export type Composant = "Shameless" | "Uchuu";

export interface VariableUchuu {
  /** Nom exact de la variable d'environnement chez `composant` (noms Compose côté Shameless). */
  nom: string;
  composant: Composant;
  role: string;
  valeur: string;
}

export interface VariableAConfirmer extends VariableUchuu {
  note: string;
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
  /** Bloc `.env` à copier dans la configuration d'Uchuu (confirmé uniquement). */
  envUchuu: string;
  /** Clé bot → API : générée et enregistrée côté Shameless, non reportée dans Uchuu pour l'instant. */
  aConfirmer: VariableAConfirmer;
}

const ROLES = {
  botUrl: "Adresse publique du bot : l'API y envoie les webhooks et lit les candidats vocaux.",
  webUrl: "Adresse publique de Shameless, utilisée pour les liens envoyés au bot.",
  botApiKey: "Clé envoyée par l'API au bot (en-tête Bearer) ; même valeur des deux côtés.",
  webhookSecret: "Secret de signature HMAC-SHA256 des webhooks ; même valeur des deux côtés.",
  guildId: "ID du serveur Discord de l'Instance, identique à DISCORD_GUILD_ID côté Shameless.",
  botToApiKey: "Clé que le bot présente à l'API sur les routes /bot/… (Bearer).",
} as const;

export function construireRecapUchuu(entree: EntreeRecapUchuu): RecapUchuu {
  if (!/^\d{17,20}$/.test(entree.guildId)) {
    throw new Error("L'ID du serveur Discord doit être un identifiant de 17 à 20 chiffres.");
  }
  const webUrl = entree.adressePublique.trim().replace(/\/+$/, "");
  const { botApiKey, webhookSecret, botToApiKey } = entree.secrets;

  const variables: VariableUchuu[] = [
    { nom: "SHAMELESS_BOT_URL", composant: "Shameless", role: ROLES.botUrl, valeur: entree.adresseBot },
    { nom: "SHAMELESS_WEB_URL", composant: "Shameless", role: ROLES.webUrl, valeur: webUrl },
    { nom: "SHAMELESS_BOT_API_KEY", composant: "Shameless", role: ROLES.botApiKey, valeur: botApiKey },
    { nom: "SHAMELESS_WEBHOOK_SECRET", composant: "Shameless", role: ROLES.webhookSecret, valeur: webhookSecret },
    { nom: "SHAMELESS_BOT_API_KEY", composant: "Uchuu", role: ROLES.botApiKey, valeur: botApiKey },
    { nom: "SHAMELESS_WEBHOOK_SECRET", composant: "Uchuu", role: ROLES.webhookSecret, valeur: webhookSecret },
    { nom: "SHAMELESS_WEB_URL", composant: "Uchuu", role: ROLES.webUrl, valeur: webUrl },
    { nom: "SHAMELESS_GUILD_ID", composant: "Uchuu", role: ROLES.guildId, valeur: entree.guildId },
  ];

  const envUchuu = [
    "# Intégration Shameless — à reporter dans la configuration d'Uchuu, puis redémarrer le bot.",
    ...variables.filter((v) => v.composant === "Uchuu").map((v) => `${v.nom}=${v.valeur}`),
    "",
  ].join("\n");

  return {
    variables,
    envUchuu,
    aConfirmer: {
      nom: "SHAMELESS_BOT_TO_API_KEY",
      composant: "Shameless",
      role: ROLES.botToApiKey,
      valeur: botToApiKey,
      note: "Nom et usage côté Uchuu à confirmer : ne pas reporter dans Uchuu tant que ce n'est pas fait.",
    },
  };
}
