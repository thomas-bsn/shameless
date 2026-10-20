# Collecte des games de SoloQ (Riot match-v5)

Script lancé à la main par le mainteneur (ticket #181, spec #168). Il enregistre dans un dossier les games classées récentes d'un joueur, avec les stats des dix joueurs de chaque game. Ce dossier est la seule sortie ; l'outil d'ajustement de la note (#185) ne travaille que dessus. Node 24, aucune dépendance d'exécution.

## Usage

```
node src/main.ts NOM#TAG --dossier ./soloq [--nombre 300] [--region euw]
```

- `NOM#TAG` : RiotId du joueur ciblé (région EUW par défaut, routage `europe`).
- `--nombre` : nombre de games classées (queue 420) les plus récentes, 100 par défaut.
- `--dossier` : dossier de sortie, créé au besoin.
- `--base-url` : racine de l'API, pour les tests contre un faux serveur. Refusée si ce n'est ni `*.api.riotgames.com` ni `localhost`.

**Clé Riot** : variable `RIOT_API_KEY`, sinon fichier `~/.config/shameless/riot-key` (la clé seule, sur une ligne). Elle n'est envoyée qu'à l'API Riot (en-tête `X-Riot-Token`), jamais écrite dans le dossier de sortie ni dans le journal, et les erreurs ne la répètent pas.
**Débit** : au plus 20 requêtes par seconde et 100 par 2 minutes (limites d'une clé Personal), sur fenêtre glissante. Sur une erreur 429, le script attend le `Retry-After` puis reprend ; les erreurs 5xx sont retentées avec une attente croissante ; au bout de 6 essais il s'arrête avec un message. Compter environ 1,2 s par game : 300 games prennent six à sept minutes.

**Reprise** : une game déjà présente dans `matches/` n'est pas retéléchargée. Relancer la même commande (éventuellement avec un `--nombre` plus grand) complète le dossier. Les fichiers sont écrits de façon atomique : une interruption ne laisse pas de JSON tronqué.

## Format du dossier de sortie

```
soloq/
  index.json
  matches/
    EUW1_7123456789.json
    ...
```

### `matches/<matchId>.json`

Le document match-v5 complet, tel que renvoyé par `GET /lol/match/v5/matches/{matchId}` (`metadata` et `info`, dont `info.participants` : les dix joueurs avec leurs stats, runes, objets, `teamPosition`, `win`, etc.). Rien n'est retiré ni transformé. Seules les games de queue 420 sont demandées.

### `index.json`

```json
{
  "cible": { "riotId": "Sam#EUW", "puuid": "..." },
  "games": [
    { "matchId": "EUW1_7123456789", "date": "2026-10-01T20:00:00.000Z", "duree": 1812, "champion": "Zed" }
  ]
}
```

- `cible` : le joueur collecté. Son `puuid` retrouve sa ligne dans `info.participants[].puuid` de chaque game.
- `games` : triées de la plus récente à la plus ancienne, une entrée par fichier de `matches/`.
  - `matchId` : nom du fichier sans `.json`.
  - `date` : début de la game, ISO 8601 UTC (`info.gameStartTimestamp`).
  - `duree` : durée en secondes (`info.gameDuration`).
  - `champion` : `championName` de la cible dans cette game.
- L'index est fusionné d'une exécution à l'autre ; relancé avec un autre joueur dans le même dossier, il repart de zéro pour ce joueur (les anciens fichiers de `matches/` restent mais ne sont plus indexés : utiliser un dossier par joueur).

## Tests

```
npm test
```

Les tests lancent le script contre un faux serveur Riot local (`test/faux-riot.ts`) : collecte, reprise, limites de débit et 429, clé absente de tout fichier et journal. Aucun appel à l'API réelle ni lecture de la vraie clé. `npm run typecheck` demande `npm install` (types Node et TypeScript, en développement seulement).
