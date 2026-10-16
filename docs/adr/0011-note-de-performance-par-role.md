# 0011 — Note de performance sur 100, par rôle et par références

Statut : acceptée — 2026-10-16

## Contexte
La Phase 1c donne à chaque Joueur une note de performance sur 100 pour chaque Game terminée,
inspirée de DPM.lol, dont la formule n'est pas publique. Le winrate seul ne dit rien du niveau
individuel, et la note alimente ensuite les stats, la fiche Joueur et le cost mensuel. Une
Instance compte trop peu de Joueurs et de games pour les comparer entre eux : il faut des
références fixes. Le Companion transmet les stats de la League Client API locale sans contrat
figé : une stat peut manquer, la durée de la game aussi, et la position jouée est souvent vide
en custom.

## Décision
La note est la moyenne pondérée des scores de six critères :

| Critère | Mesure |
|---|---|
| KDA | (kills + assists) / max(1, morts) |
| Participation aux kills | (kills + assists) / kills de l'Équipe, plafonnée à 100 % |
| CS/min | (sbires + monstres neutres) / minutes |
| Dégâts/min | dégâts infligés aux champions / minutes |
| Vision/min | score de vision / minutes |
| Objectifs/min | dégâts aux objectifs / minutes |

- **Score d'un critère** : chaque rôle fixe trois références par critère, Faible, Moyen et
  Excellent. Une valeur ≤ Faible vaut 0, = Moyen vaut 50, ≥ Excellent vaut 100 ; linéaire entre
  deux références. Une game « moyenne pour le rôle » vaut donc 50.
- **Poids par rôle** (sur 100) :

  | Rôle | KDA | KP | CS | Dégâts | Vision | Objectifs |
  |---|---|---|---|---|---|---|
  | Top | 20 | 15 | 20 | 20 | 10 | 15 |
  | Jungle | 20 | 25 | 10 | 10 | 15 | 20 |
  | Mid | 20 | 20 | 20 | 25 | 5 | 10 |
  | ADC | 20 | 15 | 25 | 25 | 5 | 10 |
  | Support | 25 | 30 | — | 10 | 35 | — |

  Rôle inconnu : références du mid, six critères à poids égaux.
- **Stats manquantes** : un critère dont une stat manque (ou la durée pour les « par minute »,
  ou les kills de l'Équipe pour la KP) est omis, et les poids des critères restants sont
  renormalisés. Sans aucun critère mesurable, pas de note (game saisie à la main, Joueur absent
  du rapport).
- **Rôle noté** : la position transmise par la partie, sinon le rôle de l'Autorole dans la
  Série, sinon le rôle principal SoloQ du Joueur. La source du rôle est exposée avec la note.
- **Explicabilité** : la note est exposée avec chaque composante (valeur mesurée, score, poids
  renormalisé), le rôle retenu et sa source.
- Les références et les poids vivent à un seul endroit
  (`api/Shameless.Application/Stats/ReferencesDeNote.cs`). La note est recalculée à la lecture
  depuis les stats brutes conservées : changer une référence s'applique à toutes les games
  passées.

Les références sont des ordres de grandeur de partie classique (SoloQ, tous niveaux), choisies
avant d'avoir un inventaire des champs réellement transmis par le Companion en production. Elles
sont à ajuster une fois cet inventaire et quelques soirées réelles disponibles.

Alternatives écartées :
- Comparer chaque Joueur aux autres Joueurs de l'Instance (percentiles) : échantillon trop
  réduit, et une note qui change quand d'autres jouent.
- Noter sans tenir compte du rôle : pénalise le support sur le CS et les dégâts.
- Utiliser l'or, les dégâts subis, les tourelles ou les dragons : redondants avec les critères
  retenus ou trop rares par game ; ils restent lus et peuvent entrer plus tard.

## Conséquences
- Ni stockage ni migration : la note se calcule à la lecture (`NotesDeLaGame.Calculer(game)`).
- Une note historique peut changer si les références évoluent ; le cost mensuel, lui, est figé
  dans `PlayerMonthlyCost` une fois le mois clos.
- Le résultat de la game n'entre pas dans la note : il est compté à part (winrate, cost).
