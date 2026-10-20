/** Au plus `max` requêtes sur toute fenêtre glissante de `fenetreMs`. */
export interface Limite {
  max: number;
  fenetreMs: number;
}

/** Clé Personal : 20 requêtes par seconde et 100 par 2 minutes. */
export const LIMITES_PERSONAL: Limite[] = [
  { max: 20, fenetreMs: 1000 },
  { max: 100, fenetreMs: 120_000 },
];

export interface OptionsClient {
  cle: string;
  baseUrl?: string | undefined;
  region?: string | undefined;
  limites?: Limite[] | undefined;
  journal: (ligne: string) => void;
}

const ROUTAGE: Record<string, string> = {
  euw: "europe", eune: "europe", tr: "europe", ru: "europe", me: "europe",
  na: "americas", br: "americas", lan: "americas", las: "americas",
  kr: "asia", jp: "asia",
  oce: "sea", ph: "sea", sg: "sea", th: "sea", tw: "sea", vn: "sea",
};

const MAX_TENTATIVES = 6;
const MARGE_MS = 25;

export function racineApi(region = "euw"): string {
  const r = ROUTAGE[region.toLowerCase()];
  if (!r) throw new Error(`Région inconnue : ${region}`);
  return `https://${r}.api.riotgames.com`;
}

const dormir = (ms: number) => new Promise<void>((ok) => setTimeout(ok, ms));

export class RiotClient {
  private readonly base: string;
  private readonly o: OptionsClient;
  private readonly limites: Limite[];
  private envois: number[] = []; // dates des requêtes envoyées, y compris celles refusées
  private pauseJusqua = 0;

  constructor(o: OptionsClient) {
    this.o = o;
    this.base = o.baseUrl ?? racineApi(o.region);
    this.limites = o.limites ?? LIMITES_PERSONAL;
  }

  /** Attend qu'une requête de plus tienne dans toutes les fenêtres, puis la réserve. */
  private async reserver(): Promise<void> {
    for (;;) {
      const now = Date.now();
      const plusLarge = Math.max(...this.limites.map((l) => l.fenetreMs));
      this.envois = this.envois.filter((t) => now - t < plusLarge);
      let attente = Math.max(0, this.pauseJusqua - now);
      for (const l of this.limites) {
        const dansFenetre = this.envois.filter((t) => now - t < l.fenetreMs);
        if (dansFenetre.length >= l.max) {
          const pivot = dansFenetre[dansFenetre.length - l.max]!;
          attente = Math.max(attente, pivot + l.fenetreMs - now + MARGE_MS);
        }
      }
      if (attente <= 0) break;
      await dormir(attente);
    }
  }

  /** GET JSON ; `undefined` pour un 404. Respecte les limites et le Retry-After des 429. */
  async get<T>(chemin: string): Promise<T | undefined> {
    const lisible = chemin.split("?")[0];
    for (let tentative = 1; ; tentative++) {
      await this.reserver();
      const rep = await fetch(this.base + chemin, { headers: { "X-Riot-Token": this.o.cle } });
      // Les requêtes sont séquentielles : la date de la réponse majore celle de l'arrivée chez Riot,
      // ce qui garde la fenêtre sûre même quand la latence varie.
      this.envois.push(Date.now());
      if (rep.status === 404) return undefined;
      if (rep.ok) return (await rep.json()) as T;
      const retriable = rep.status === 429 || rep.status >= 500;
      if (!retriable || tentative >= MAX_TENTATIVES) {
        throw new Error(`Riot a répondu ${rep.status} pour ${lisible}`);
      }
      const retryAfter = Number(rep.headers.get("retry-after"));
      const secondes = rep.status === 429 && Number.isFinite(retryAfter) && retryAfter >= 0 ? retryAfter : 2 ** tentative;
      this.o.journal(`Riot a répondu ${rep.status}, nouvel essai dans ${secondes} s`);
      this.pauseJusqua = Date.now() + secondes * 1000 + MARGE_MS;
    }
  }
}
