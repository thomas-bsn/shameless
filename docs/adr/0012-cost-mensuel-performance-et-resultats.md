# 0012 — Cost mensuel : performance individuelle et résultats, à partir de cinq games

Statut : acceptée — 2026-10-16

## Contexte
Le cost (1 à 6, tiers D à SS) équilibre les équipes du Picker. Jusqu'ici, il était recalculé après
chaque game depuis le Peak déclaré, ajusté par le winrate du mois, et rien n'était conservé :
`PlayerMonthlyCost` n'était jamais écrit. Le winrate seul ne dit rien du niveau individuel : un
Joueur porté par une équipe dominante gagne souvent sans bien jouer. La Phase 1c donne une note de
performance sur 100 par Game (ADR 0011) ; le cost doit en tenir compte davantage que du winrate,
partir d'une base stable plutôt que d'une remise à zéro, et garder son historique.

## Décision
**Cost du mois** = cost de départ + ajustement de performance + ajustement de résultats, arrondi au
dixième et borné entre 1 et 6.

- **Ajustement de performance** = 1 × 0,7 × (note moyenne − 50) / 50.
- **Ajustement de résultats** = 1 × 0,3 × (winrate − 50 %) / 50 %.

Soit au plus ±1 point par mois : +0,7 pour une note moyenne de 100, +0,3 pour 100 % de victoires.
Une note de 50 (« moyenne pour le rôle ») et 50 % de victoires laissent le cost inchangé. Exemple :
départ 3, note moyenne 70, 4 victoires sur 5 → 3 + 0,28 + 0,18 = 3,46 → 3,5. Un Joueur noté 80 qui
perd tout gagne 0,12 ; un Joueur noté 20 qui gagne tout en perd 0,12.

- **Seuil** : avant cinq Games terminées dans le mois, le cost reste le cost de départ ; à partir de
  la cinquième, chaque Game terminée le recalcule sur toutes les Games du mois.
- **Games comptées** : les Games terminées lancées dans le mois (date de lancement), y compris
  celles d'une Série interrompue. Une game en attente ne compte pas. Une Game sans note (saisie à la
  main, Joueur absent du rapport) compte pour le winrate ; la moyenne des notes porte sur les seules
  Games notées, et sans aucune note l'ajustement de performance est nul.
- **Cost de départ** : le cost final du dernier mois conservé avant ce mois (le mois précédent, ou
  le dernier mois joué). Sans historique, le cost du Rang SoloQ actuel (pas le Peak) via le barème
  MMR existant (Iron, Bronze → 1 ; Silver, Gold → 2 ; Platinum → 3 ; Emerald, Diamond → 4 ;
  Master → 5 ; Grandmaster, Challenger → 6 ; non classé → 1), figé au premier calcul du mois. Un nouveau Joueur reçoit ce cost dès sa création.
- **Historique** : après chaque Game terminée, le cost du mois est enregistré dans
  `PlayerMonthlyCost` (une ligne par Joueur et par mois : cost, cost de départ, Games terminées,
  victoires, note moyenne). La ligne du mois passé reste : c'est son cost final. Le cost actuel du
  Joueur (`Player.CurrentCost`) est celui de son mois le plus récent.
- **Explicabilité** : `GET /players/{id}/cost` donne le cost du mois en cours avec son détail (cost
  de départ et sa source, Games, winrate, note moyenne, les deux ajustements, le seuil) ;
  `GET /players/{id}/cost/historique` donne les mois conservés.
- Les seuils, poids et bornes vivent à un seul endroit
  (`api/Shameless.Application/Cost/ReglesDuCost.cs`) ; le calcul est `CostMensuel.Calculer`, pur.

Alternatives écartées :
- Remettre le cost à zéro chaque mois (ancien glossaire) : un Joueur repartirait de son rang SoloQ,
  sans rapport avec son niveau en customs.
- Partir du Peak : déclaré à la main et ancien ; le Rang SoloQ actuel est mesuré.
- Pondérer par un facteur de confiance selon le nombre de games : le seuil de cinq games et la
  variation plafonnée à ±1 par mois suffisent, et restent lisibles.
- Comparer les Joueurs de l'Instance entre eux : échantillon trop réduit (comme pour l'ADR 0011).

## Conséquences
- Le cost évolue d'au plus un point par mois : changer de tier prend plusieurs mois réguliers.
- Le cost d'un mois clos est figé dans `PlayerMonthlyCost`, même si les références de la note
  évoluent ensuite.
- Un cost fixé à la main par un admin reste valable jusqu'à la prochaine Game terminée du Joueur,
  qui le recalcule.
- Les colonnes `base_mmr` et `winrate_adjustment` de l'ancien modèle restent pour les mois repris de
  l'ancienne application (ADR 0007) ; elles valent 0 pour les mois calculés depuis la Phase 1c.
- Les valeurs (0,7 / 0,3, ±1, cinq games) sont initiales, à ajuster après quelques mois réels.
