import { createServer, type Server } from "node:http";
import type { AddressInfo } from "node:net";

export const CLE = "RGAPI-cle-de-test-0000-1111-2222";

export interface FauxMatch {
  id: string;
  queue: number;
  doc: unknown;
}

export interface Requete {
  chemin: string;
  temps: number;
}

export interface FauxRiot {
  url: string;
  requetes: Requete[];
  /** Les n prochaines réponses seront des 429 avec cet en-tête Retry-After (secondes). */
  refuser429(n: number, retryAfter: number): void;
  fermer(): Promise<void>;
}

/** Un faux serveur Riot : comptes par RiotId et match-v5, filtré par queue comme le vrai. */
export async function demarrerFauxRiot(opts: {
  comptes: Record<string, string>; // "Nom#TAG" -> puuid
  matches: Record<string, FauxMatch[]>; // puuid -> games (plus récente d'abord)
}): Promise<FauxRiot> {
  const requetes: Requete[] = [];
  let refus = { n: 0, retryAfter: 0 };
  const tous = new Map<string, FauxMatch>();
  for (const l of Object.values(opts.matches)) for (const m of l) tous.set(m.id, m);

  const serveur: Server = createServer((req, res) => {
    const u = new URL(req.url ?? "/", "http://x");
    const cle = req.headers["x-riot-token"];
    requetes.push({ chemin: u.pathname + u.search, temps: Date.now() });
    const json = (code: number, corps: unknown) => {
      res.writeHead(code, { "content-type": "application/json" });
      res.end(JSON.stringify(corps));
    };
    if (cle !== CLE) return json(403, { status: { status_code: 403, message: "Forbidden" } });
    if (refus.n > 0) {
      refus.n--;
      res.writeHead(429, { "retry-after": String(refus.retryAfter), "content-type": "application/json" });
      return res.end("{}");
    }
    let m: RegExpMatchArray | null;
    if ((m = u.pathname.match(/^\/riot\/account\/v1\/accounts\/by-riot-id\/([^/]+)\/([^/]+)$/))) {
      const nom = decodeURIComponent(m[1]!);
      const tag = decodeURIComponent(m[2]!);
      const puuid = opts.comptes[`${nom}#${tag}`];
      if (!puuid) return json(404, { status: { status_code: 404 } });
      return json(200, { puuid, gameName: nom, tagLine: tag });
    }
    if ((m = u.pathname.match(/^\/lol\/match\/v5\/matches\/by-puuid\/([^/]+)\/ids$/))) {
      const queue = u.searchParams.get("queue");
      const start = Number(u.searchParams.get("start") ?? 0);
      const count = Number(u.searchParams.get("count") ?? 20);
      if (count > 100) return json(400, {});
      const ids = (opts.matches[m[1]!] ?? [])
        .filter((g) => queue === null || String(g.queue) === queue)
        .slice(start, start + count)
        .map((g) => g.id);
      return json(200, ids);
    }
    if ((m = u.pathname.match(/^\/lol\/match\/v5\/matches\/([^/]+)$/))) {
      const g = tous.get(m[1]!);
      return g ? json(200, g.doc) : json(404, {});
    }
    json(404, {});
  });
  await new Promise<void>((ok) => serveur.listen(0, "127.0.0.1", ok));
  const port = (serveur.address() as AddressInfo).port;
  return {
    url: `http://127.0.0.1:${port}`,
    requetes,
    refuser429(n, retryAfter) {
      refus = { n, retryAfter };
    },
    fermer: () => new Promise((ok) => serveur.close(() => ok())),
  };
}

/** Un document match-v5 minimal mais de la bonne forme (dix joueurs, la cible en premier). */
export function faireMatch(id: string, puuidCible: string, champion: string, debut: number, duree: number, queue = 420): FauxMatch {
  const participants = Array.from({ length: 10 }, (_, i) => ({
    puuid: i === 0 ? puuidCible : `autre-${id}-${i}`,
    championName: i === 0 ? champion : `Champ${i}`,
    kills: i,
    deaths: 1,
    assists: 2,
    teamId: i < 5 ? 100 : 200,
    win: i < 5,
  }));
  return {
    id,
    queue,
    doc: {
      metadata: { matchId: id, participants: participants.map((p) => p.puuid) },
      info: { gameStartTimestamp: debut, gameDuration: duree, queueId: queue, participants },
    },
  };
}
