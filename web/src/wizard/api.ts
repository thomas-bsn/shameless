// Client HTTP du serveur du wizard (conteneur `wizard`, joint via le proxy sous /_setup/api).
export type Phase = "locked" | "configuring" | "installing" | "done" | "failed";

/** Récapitulatif Uchuu construit par le serveur du wizard (valeurs en clair : à ne jamais journaliser). */
export interface RecapUchuu {
  variables: { nom: string; composant: "Shameless" | "Uchuu"; role: string; valeur: string }[];
  envUchuu: string;
  aSaisirAMain: { nom: string; facultatif: boolean; role: string }[];
  docBot: string;
  verification: string;
}

export interface WizardState {
  phase: Phase;
  publicUrl?: string;
  failure?: string;
  codeExpired?: boolean;
  warnings?: string[];
  uchuu?: RecapUchuu;
  /** Compte Discord connecté par l'Opérateur : le premier Superadministrateur. */
  discord?: { id: string; username: string };
  /** Pourquoi la dernière connexion Discord a échoué (expliqué en français par le serveur). */
  discordError?: string;
}

export type Values = {
  publicUrl: string;
  clientId: string;
  clientSecret: string;
  guildId: string;
  riotKey: string;
  timezone: string;
  uchuu: boolean;
  botUrl: string;
  /** Facultatif, affiché seulement par le wizard : « Bot Discord » quand il est vide. */
  botName: string;
};
export type FieldErrors = Partial<Record<keyof Values | "discord", string>>;

export interface Reply<T> {
  status: number;
  body: T & { message?: string };
}

async function call<T>(method: "GET" | "POST", route: string, body?: unknown): Promise<Reply<T>> {
  const res = await fetch(`/_setup/api/${route}`, {
    method,
    headers: body === undefined ? undefined : { "content-type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
    credentials: "same-origin",
  });
  const text = await res.text();
  let parsed: unknown = {};
  try {
    parsed = text ? JSON.parse(text) : {};
  } catch {
    // Réponse non JSON (proxy en erreur par exemple) : traitée comme vide.
  }
  return { status: res.status, body: parsed as Reply<T>["body"] };
}

export const getState = () => call<WizardState>("GET", "state");
export const unlock = (code: string) => call<{ phase: Phase }>("POST", "unlock", { code });
export const validate = (values: Values) => call<{ errors: FieldErrors; warnings?: string[]; callbackUrl: string | null; wizardRedirectUri?: string | null }>("POST", "validate", values);
export const install = (values: Values) => call<{ errors?: FieldErrors; warnings?: string[]; uchuu?: RecapUchuu }>("POST", "install", values);
export const startDiscord = (values: Pick<Values, "publicUrl" | "clientId" | "clientSecret">) =>
  call<{ authorizeUrl?: string; redirectUri?: string; errors?: FieldErrors }>("POST", "discord/start", values);
