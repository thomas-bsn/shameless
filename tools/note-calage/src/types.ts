/** Un joueur d'une game match-v5 (`info.participants[]`) : seuls les champs lus par l'outil sont typés. */
export type Participant = {
  puuid?: string;
  championName: string;
  teamId?: number;
  teamPosition?: string;
  individualPosition?: string;
  win?: boolean;
  kills?: number;
  deaths?: number;
  assists?: number;
  totalMinionsKilled?: number;
  neutralMinionsKilled?: number;
  totalDamageDealtToChampions?: number;
  visionScore?: number;
  damageDealtToObjectives?: number;
  goldEarned?: number;
};

/** Une game du dossier de collecte, réduite à ce qui sert à l'appariement et à la note. */
export type Game = {
  matchId: string;
  /** Début de la game, en millisecondes depuis l'epoch (`info.gameStartTimestamp`). */
  debut: number;
  /** Durée en secondes (`info.gameDuration`). */
  duree: number;
  participants: Participant[];
};

export type DossierDeCollecte = {
  cible?: { riotId: string; puuid: string };
  games: Game[];
};

/** Une ligne du fichier de scores dpm.lol saisi à la main. */
export type LigneDeScore = {
  /** Numéro de la ligne dans le fichier (1 = première ligne de données). */
  numero: number;
  /** Date en millisecondes depuis l'epoch (UTC). */
  date: number;
  /** Durée en secondes. */
  duree: number;
  champion: string;
  kills: number;
  deaths: number;
  assists: number;
  score: number;
  /** Place dans la game (1 = meilleur), si saisie. */
  place?: number;
};
