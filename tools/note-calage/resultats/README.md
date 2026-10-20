# Résultats du calage de la note (ticket #182)

`calage-2026-10-20.json` : paramètres ajustés par `tools/note-calage` sur 743 lignes (cinq rôles) : 300 games de SoloQ du mainteneur (`tools/riot-collecte`) et les scores dpm.lol de ses games, lus dans deux exports HAR faits par lui dans son navigateur (645 games avec son score, 51 games dépliées avec les dix scores). Le fichier ne contient que des paramètres et des mesures, aucune donnée de joueur.

Écart moyen avec dpm.lol sur des games mises de côté (4 tirages), note actuelle de l'API puis note calée :

| Rôle | Lignes | Avant | Après |
|---|---|---|---|
| Top | 163 | 13,8 | 6,9 |
| Jungle | 101 | 10,9 | 6,8 |
| Mid | 264 | 16,1 | 6,1 |
| ADC | 102 | 15,2 | 9,0 |
| Support | 113 | 12,9 | 6,8 |

Tous rôles confondus : 14,6 avant, 7,1 après ; 85 % puis 91 % des paires de joueurs d'une même game rangées dans le même ordre.

Limites : une seule source de joueur cible (surtout du Mid), des games de SoloQ et pas de customs, et des références qui se sont parfois rapprochées jusqu'à devenir un palier (faible ≈ moyen ≈ excellent) : à lisser avant de les brancher.
