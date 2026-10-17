/**
 * Secrets de l'intégration Uchuu (ADR 0013, spec #72) : trois valeurs distinctes, hexadécimales,
 * de 32 octets aléatoires cryptographiques (équivalent de `openssl rand -hex 32`).
 * Ce module ne journalise rien : l'appelant ne doit jamais loguer l'objet rendu.
 */

export interface SecretsUchuu {
  /** `SHAMELESS_BOT_API_KEY` : Bearer envoyé par l'API au bot. */
  botApiKey: string;
  /** `SHAMELESS_WEBHOOK_SECRET` : secret HMAC-SHA256 des webhooks. */
  webhookSecret: string;
  /** `SHAMELESS_BOT_TO_API_KEY` : Bearer que le bot présente à l'API sur `/bot/…`. */
  botToApiKey: string;
}

export const OCTETS_PAR_SECRET = 32;

/** Source d'octets aléatoires ; par défaut le CSPRNG du runtime. Injectable pour les tests. */
export type SourceAleatoire = (octets: number) => Uint8Array;

const sourceCryptographique: SourceAleatoire = (octets) => globalThis.crypto.getRandomValues(new Uint8Array(octets));

function enHex(octets: Uint8Array): string {
  return Array.from(octets, (o) => o.toString(16).padStart(2, "0")).join("");
}

export function genererSecretsUchuu(source: SourceAleatoire = sourceCryptographique): SecretsUchuu {
  const deja = new Set<string>();
  const tirer = (): string => {
    for (;;) {
      const octets = source(OCTETS_PAR_SECRET);
      if (octets.length < OCTETS_PAR_SECRET) throw new Error("Source aléatoire insuffisante pour générer un secret.");
      const valeur = enHex(octets);
      if (!deja.has(valeur)) {
        deja.add(valeur);
        return valeur;
      }
    }
  };
  return { botApiKey: tirer(), webhookSecret: tirer(), botToApiKey: tirer() };
}
