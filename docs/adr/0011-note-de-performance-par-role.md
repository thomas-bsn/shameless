# 0011 — Note de performance sur 100, par rôle et par références

Statut : acceptée — 2026-10-16 ; révisée — 2026-10-20 (calage sur dpm.lol, ticket #182)

## Contexte
La Phase 1c donne à chaque Joueur une note de performance sur 100 pour chaque Game terminée,
inspirée de DPM.lol, dont la formule n'est pas publique. Le winrate seul ne dit rien du niveau
individuel, et la note alimente ensuite les stats, la fiche Joueur et le cost mensuel. Une
Instance compte trop peu de Joueurs et de games pour les comparer entre eux : il faut des
références fixes. Le Companion transmet les stats de la League Client API locale sans contrat
figé : une stat peut manquer, la durée de la game aussi, et la position jouée est souvent vide
en custom.

La première version (six critères, références choisies à la main) s'écartait de dpm.lol de 11 à
16 points en moyenne. Elle a été calée sur des scores réels (voir « Révision »).

## Décision
La note est la moyenne pondérée des scores de dix critères et d'une composante victoire :

| Critère | Mesure |
|---|---|
| KDA | (kills + assists) / max(1, morts) |
| Participation aux kills | (kills + assists) / kills de l'Équipe, plafonnée à 100 % |
| CS/min | (sbires + monstres neutres) / minutes |
| Dégâts/min | dégâts infligés aux champions / minutes |
| Vision/min | score de vision / minutes |
| Objectifs/min | dégâts aux objectifs / minutes |
| Or/min | or gagné / minutes |
| Part des kills | kills du Joueur / kills de l'Équipe |
| Part des dégâts | dégâts aux champions du Joueur / ceux de l'Équipe |
| Part de l'or | or du Joueur / or de l'Équipe |
| Victoire | score 100 si l'Équipe du Joueur a gagné la game, 0 sinon (pas de référence) |

- **Score d'un critère** : chaque rôle fixe trois références par critère, Faible, Moyen et
  Excellent. Une valeur ≤ Faible vaut 0, = Moyen vaut 50, ≥ Excellent vaut 100 ; linéaire entre
  deux références.
- **Poids par rôle** (relatifs : seule la proportion compte ; somme de 100 à l'arrondi près) :

  | Rôle | KDA | KP | CS | Dégâts | Vision | Objectifs | Or | Part kills | Part dégâts | Part or | Victoire |
  |---|---|---|---|---|---|---|---|---|---|---|---|
  | Top | 32 | 1 | 12 | 11 | 4 | 11 | 11 | 1 | 5 | 1 | 11 |
  | Jungle | 21 | 7 | 9 | 4 | 9 | 17 | 13 | 11 | 1 | 1 | 8 |
  | Mid | 23 | 1 | 9 | 20 | 4 | 12 | 11 | 4 | 1 | 1 | 15 |
  | ADC | 25 | 1 | 14 | 12 | 7 | 18 | 13 | 1 | 1 | 1 | 7 |
  | Support | 23 | 10 | — | 11 | 21 | 4 | 17 | 1 | 1 | 1 | 12 |

  Rôle inconnu : références du Mid, tous les critères et la victoire à poids égal. Les
  références de chaque rôle sont dans `ReferencesDeNote.cs`. Une game « moyenne pour le rôle »
  vaut 50 hors victoire.
- **Stats manquantes** : un critère dont une stat manque (ou la durée pour les « par minute »,
  ou les kills de l'Équipe pour la KP et la part des kills) est omis, et les poids des critères
  restants sont renormalisés. Les parts de dégâts et d'or demandent les stats de tous les
  participants de l'Équipe (une part sur un total partiel serait gonflée) ; la victoire demande
  que la game ait un vainqueur. Sans aucun critère mesurable (la victoire seule ne suffit pas),
  pas de note : game saisie à la main, Joueur absent du rapport.
- **Rôle noté** : la position transmise par la partie, sinon le rôle de l'Autorole dans la
  Série, sinon le rôle principal SoloQ du Joueur. La source du rôle est exposée avec la note.
- **Explicabilité** : la note est exposée avec chaque composante (valeur mesurée, score, poids
  renormalisé, victoire comprise), le rôle retenu et sa source.
- Les références et les poids vivent à un seul endroit
  (`api/Shameless.Application/Stats/ReferencesDeNote.cs`). La note est recalculée à la lecture
  depuis les stats brutes conservées : changer une référence, un poids ou la façon de noter
  s'applique à toutes les games passées, et corriger le vainqueur d'une game change la note de
  ses participants.

## Révision du 2026-10-20 : calage sur dpm.lol
Les nouveaux critères et la composante victoire viennent d'un calage réel (ticket #182, outil
`tools/note-calage`, résultats dans `tools/note-calage/resultats/`) :

- **Données** : 300 games de SoloQ d'un joueur cible et 743 lignes de scores dpm.lol appariées à
  ces games (cinq rôles : Top 163, Jungle 101, Mid 264, ADC 102, Support 113), dont des games avec
  les dix scores.
- **Ce que dpm.lol pèse** : l'or par minute et le résultat, que la note d'origine ignorait.
  D'où l'or par minute, les trois parts de l'Équipe et la victoire. La note d'origine disait
  « le résultat de la game n'entre pas dans la note » : c'est renversé, la victoire pèse 7 à 15 %
  selon le rôle. Le cost garde son propre ajustement de résultats (winrate), ce qui compte
  deux fois la victoire dans le cost : à surveiller.
- **Ajustement puis lissage** : les références et poids ont été ajustés rôle par rôle, puis
  lissés. L'ajustement libre rapproche parfois Faible, Moyen et Excellent jusqu'à un palier
  (dégâts/min du Top : 0 / 824 / 830) : le lissage impose un écart d'au moins 15 % de Moyen de
  chaque côté (12 % en Jungle, où 15 % dégradait l'écart de plus de 1,5 point sur un tirage) et
  un poids d'au moins 1 sur chaque critère, sauf le CS du Support.
- **Écart moyen avec dpm.lol**, note d'origine puis note lissée, moyenne de quatre tirages de
  games mises de côté : Top 13,8 → 6,5 ; Jungle 10,9 → 5,8 ; Mid 16,1 → 6,4 ; ADC 15,2 → 7,3 ;
  Support 12,9 → 6,2. Ces chiffres sont un peu optimistes : les paramètres ont été ajustés sur
  toutes les lignes, donc seul le tirage 1 est réellement mis de côté (Top 7,1 ; Jungle 6,7 ;
  Mid 7,1 ; ADC 8,2 ; Support 5,2), et l'ajustement refait à chaque tirage donnait 6,1 à 9,0
  selon le rôle. Tous rôles confondus : 7,4 points sur le tirage 1, contre 14,6 pour la note
  d'origine ; 91 % des paires de joueurs d'une même game rangées dans le même ordre, contre 85 %.
  Le lissage coûte 0,2 à 1,3 point par rôle par rapport à l'ajustement libre. Détail par rôle et
  par tirage dans le README des résultats.
- **Limites** : une seule source de joueur cible (surtout du Mid : les autres rôles viennent de
  ses coéquipiers et adversaires), des games de SoloQ et pas des customs (parties à 5 contre 5
  entre amis, souvent sans position ni durée), pas de composante de lane (écart d'or ou de CS à
  15 minutes, que dpm.lol pèse sans doute) ni de timeline. Les références sont celles de dpm.lol
  sur la SoloQ du joueur cible, pas un niveau absolu. Le rôle inconnu n'est pas mesuré. Les
  références du Support sur les dégâts (159 à 1 064 par minute) et les poids de quelques critères
  rares (poids 1) sont peu contraints.

Alternatives écartées :
- Comparer chaque Joueur aux autres Joueurs de l'Instance (percentiles) : échantillon trop
  réduit, et une note qui change quand d'autres jouent.
- Noter sans tenir compte du rôle : pénalise le support sur le CS et les dégâts.
- Garder les références ajustées telles quelles : écart un peu plus bas sur l'échantillon, mais
  des paliers (une valeur proche de Moyen passe de 0 à 100) qui rendent la note instable hors de
  l'échantillon.
- Utiliser les dégâts subis, les tourelles ou les dragons : redondants avec les critères
  retenus ou trop rares par game ; ils restent lus et peuvent entrer plus tard.

## Conséquences
- Ni stockage ni migration : la note se calcule à la lecture (`NotesDeLaGame.Calculer(game)`).
- Une note historique peut changer si les références évoluent ; le cost mensuel, lui, est figé
  dans `PlayerMonthlyCost` une fois le mois clos.
- Le résultat de la game entre dans la note (composante victoire) ; il est aussi compté à part
  (winrate, cost).
- Un Administrateur peut corriger les stats d'un joueur dans une game terminée (champion, voie,
  K/D/A, CS, or, dégâts, vision, note). Les corrections sont stockées à part, une par champ et par
  joueur (`corrections_de_stats`) ; le JSON du Companion reste intact. Toute lecture passe par
  `ValeursEffectives` : la note se calcule sur les stats corrigées, ou prend la note corrigée ; le
  cost du mois du joueur est recalculé à chaque correction ou annulation.
