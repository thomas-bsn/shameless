# 0004 — Les stats des customs viennent du Companion

Statut : acceptée — 2026-10-15 ; révisée le 2026-10-20 (spike #158) : source des données et périmètre précisés

## Contexte
L'API Riot ne renvoie pas les données des parties personnalisées, sauf celles créées avec
un tournament code. La Tournament API exige une clé Production, demandée depuis 9 mois et
toujours en attente. Seule la clé Personal est disponible. Jusqu'ici, les résultats étaient
saisis à la main.

## Décision
Une app PC, le Companion (.NET 10, Avalonia), lit les stats de fin de game des dix joueurs
via l'API locale du client LoL (lockfile et WebSocket) et les envoie à l'API Shameless.
Un seul joueur équipé par game suffit. L'API dédoublonne si plusieurs joueurs envoient la
même game. La saisie manuelle reste disponible en secours.
La clé Personal ne sert qu'aux rangs SoloQ et aux identités Riot.

## Révision du 2026-10-20 (spike #158)
Une capture réelle (custom 5v5 sur la Faille, un joueur contre des bots) a précisé la source :
- **Source** : l'événement `/lol-end-of-game/v1/eog-stats-block`, émis à l'écran de fin de game. Il
  donne `gameId`, `gameType` (`CUSTOM_GAME`), les deux équipes (`teamId` 100 = blue, 200 = red,
  `isWinningTeam`) et, pour chacun des dix joueurs, `puuid`, `championId` et 183 stats, dont les objectifs
  (tourelles, dragons, barons, inhibiteurs). L'événement arrive deux fois : on dédoublonne par `gameId`.
- **`platformId`** n'est pas dans ce bloc. Il vient de l'historique du client
  (`/lol-match-history/v1/games/{gameId}`, qui peut mettre 5 à 15 s à répondre : lecture avec relances).
  L'identifiant du Rapport est `<platformId>_<gameId>`.
- **Secrets** : le bloc contient des jetons JWT et un mot de passe de chat. Le Rapport est construit
  par liste blanche de champs, jamais par copie du bloc ; les captures versionnées sont anonymisées.
- **Périmètre** : le Companion ne sert qu'aux customs 5v5. Toute autre partie (normale, classée, ARAM,
  nombre de joueurs différent de dix) est ignorée, sans Rapport.
- **Pas encore vérifié** : une custom à dix joueurs humains (le PUUID de chaque autre humain). Si un PUUID
  manque, le Rapport est en revue côté API (Rapport en revue) : la game ne se corrompt pas, la saisie
  manuelle et l'import d'un fichier (ADR 0020) restent le secours.

## Conséquences
- Pas de stats si aucun joueur de la game n'a lancé le Companion.
- L'API locale du client LoL n'est pas officielle et peut changer à un patch : son code est
  isolé dans un module.
- Si la clé Production arrive, match-v5 et les tournament codes pourront compléter ou
  remplacer le Companion. Cette décision sera alors revue.
