# 0004 — Les stats des customs viennent du Companion

Statut : acceptée — 2026-10-15

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

## Conséquences
- Pas de stats si aucun joueur de la game n'a lancé le Companion.
- L'API locale du client LoL n'est pas officielle et peut changer à un patch : son code est
  isolé dans un module.
- Si la clé Production arrive, match-v5 et les tournament codes pourront compléter ou
  remplacer le Companion. Cette décision sera alors revue.
