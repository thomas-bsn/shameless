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

## Paramètres lissés, branchés dans l'API

`calage-2026-10-20-lisse.json` : les paramètres ajustés ci-dessus, lissés par `src/lisser.ts` (`node src/lisser.ts calage-2026-10-20.json lisse.json --ecart-role JUNGLE=0.12`), avec leurs mesures. C'est ce fichier que reprend `api/Shameless.Application/Stats/ReferencesDeNote.cs`.

Pourquoi lisser : l'ajustement libre a rapproché faible, moyen et excellent jusqu'à un palier (dégâts/min du Top : 0 / 824 / 830 ; vision/min du Mid : 0 / 0,85 / 0,86), ce qui fait passer la note de 0 à 100 sur quelques points de valeur. Règles :

- Faible <= Moyen x (1 - e) et Excellent >= Moyen x (1 + e), avec e = 15 % ; Jungle : e = 12 % (à 15 %, son écart sur le test se dégradait de 1,6 point sur un tirage) ;
- chaque critère et la victoire gardent un poids d'au moins 1 sur 100, sauf le CS du Support, jamais noté ; poids arrondis à l'entier ;
- références arrondies (3 décimales sous 1, 2 sous 20, sinon l'unité).

Écart moyen avec dpm.lol sur les games mises de côté, par rôle, pour quatre tirages (graines 1 à 4 ; seule la graine 1 est le tirage de l'ajustement, les autres recoupent ses games d'apprentissage : lire la différence entre les deux colonnes plus que le niveau). Pour mémoire, la note d'origine donne 15,1 / 13,8 / 12,3 / 14,2 en Top, 10,7 / 10,8 / 11,3 / 10,9 en Jungle, 16,9 / 16,0 / 14,4 / 17,3 en Mid, 14,3 / 15,3 / 16,4 / 15,0 en ADC et 11,4 / 13,4 / 14,0 / 12,9 en Support :

| Rôle | Ajusté | Lissé | Lissé - ajusté (moyenne, maximum) |
|---|---|---|---|
| Top | 5,9 / 4,9 / 5,9 / 5,8 | 7,1 / 5,5 / 6,8 / 6,4 | +1,0, +1,2 |
| Jungle | 5,4 / 5,5 / 4,3 / 3,9 | 6,7 / 6,1 / 5,5 / 4,9 | +1,0, +1,3 |
| Mid | 6,4 / 5,2 / 5,3 / 4,7 | 7,1 / 5,5 / 5,7 / 5,2 | +0,5, +0,7 |
| ADC | 8,0 / 7,6 / 6,7 / 6,2 | 8,2 / 7,4 / 6,9 / 6,5 | +0,2, +0,3 |
| Support | 5,3 / 4,9 / 6,7 / 7,2 | 5,2 / 5,2 / 7,0 / 7,5 | +0,3, +0,3 |

Tous rôles confondus, graine 1 : 7,4 points sur le test (7,1 ajusté), 5,7 sur l'apprentissage (5,1 ajusté), 91 % des paires de joueurs dans le bon ordre (91 % ajusté).

Non mesuré : le rôle inconnu (références du Mid lissé, tous les poids à 1, victoire comprise), faute de lignes sans rôle dans les données.
