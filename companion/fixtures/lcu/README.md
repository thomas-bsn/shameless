# Fixtures du client LoL (LCU)

Captures réelles de fin de game, faites avec l'outil de `companion/spike/` puis anonymisées
(`shameless-spike anonymize`) : pseudos, tags, PUUID, ids de compte et jetons JWT remplacés.
Les stats, les champions et le `gameId` sont ceux de la partie.

| Dossier | Partie |
|---|---|
| `custom-contre-bots-5v5/` | Custom 5v5 sur la Faille (`queueId` 3100), un joueur humain contre des bots, 25 min. Équipe 100 victorieuse. |

Chaque dossier contient `eog-stats-block.json` (bloc de fin de game, `/lol-end-of-game/v1/eog-stats-block`)
et `match-history-game.json` (`/lol-match-history/v1/games/{gameId}`).

Limites connues : l'anonymiseur remplace aussi un nom de champion quand un bot porte ce nom
(`championName` devient `JoueurN` ; `championId` reste exact). Une custom à dix joueurs humains reste à capturer.
