export interface Limites {
  parSeconde: number;
  parFenetre: number;
  fenetreMs: number;
}

export interface OptionsClient {
  cle: string;
  baseUrl?: string | undefined;
  region?: string | undefined;
  limites?: Limites | undefined;
  journal: (ligne: string) => void;
}

const ROUTAGE: Record<string, string> = {
  euw: "europe", eune: "europe", tr: "europe", ru: "europe", me: "europe",
  na: "americas", br: "americas", lan: "americas", las: "americas",
  kr: "asia", jp: "asia",
  oce: "sea", ph: "sea", sg: "sea", th: "sea", tw: "sea", vn: "sea",
};

export function racineApi(region = "euw"): string {
  const r = ROUTAGE[region.toLowerCase()];
  if (!r) throw new Error(`Région inconnue : ${region}`);
  return `https://${r}.api.riotgames.com`;
}

export class RiotClient {
  private readonly base: string;
  private readonly o: OptionsClient;

  constructor(o: OptionsClient) {
    this.o = o;
    this.base = o.baseUrl ?? racineApi(o.region);
  }

  /** GET JSON ; `undefined` pour un 404. */
  async get<T>(chemin: string): Promise<T | undefined> {
    const rep = await fetch(this.base + chemin, { headers: { "X-Riot-Token": this.o.cle } });
    if (rep.status === 404) return undefined;
    if (!rep.ok) throw new Error(`Riot a répondu ${rep.status} pour ${chemin.split("?")[0]}`);
    return (await rep.json()) as T;
  }
}
