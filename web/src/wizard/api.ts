// Client HTTP du serveur du wizard (conteneur `wizard`, joint via le proxy sous /_setup/api).
export type Phase = "locked" | "configuring" | "installing" | "done" | "failed";

export interface WizardState {
  phase: Phase;
  publicUrl?: string;
  failure?: string;
  codeExpired?: boolean;
  warnings?: string[];
}

export type Values = {
  publicUrl: string;
  clientId: string;
  clientSecret: string;
  guildId: string;
  adminIds: string;
};
export type FieldErrors = Partial<Record<keyof Values, string>>;

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
export const validate = (values: Values) => call<{ errors: FieldErrors; callbackUrl: string | null }>("POST", "validate", values);
export const install = (values: Values) => call<{ errors?: FieldErrors; warnings?: string[] }>("POST", "install", values);
