# 0009 — Arrêter une série garde les games jouées

Statut : acceptée — 2026-10-15

## Contexte
L'ancienne version avait un seul « abandon » de série, qui supprimait la série et toutes
ses games. Il servait à deux cas différents : une série créée par erreur, et une série
arrêtée en plein BO parce qu'un joueur part. Dans le second cas, les games ont réellement
été jouées. À partir de la Phase 1b, elles portent les stats des dix joueurs envoyées par
le Companion ; en Phase 2, les votes MVP/DOFD et les clips.

## Décision
Deux actions distinctes, derrière un seul bouton « Arrêter la série » :
- **Annuler** : la série n'a aucune game terminée. Elle est effacée avec ses games.
- **Interrompre** : la série a au moins une game terminée. Elle se termine sans vainqueur ;
  ses games terminées restent et comptent pour les stats, le cost, les votes et les clips.

Une série terminée (gagnée, nulle ou interrompue) ne peut plus être annulée. Une game
terminée saisie par erreur se corrige ou se supprime à part.

Alternative écartée : garder « abandonner = tout effacer », qui perd des stats, des votes
et des clips de parties réellement jouées et fait revenir le cost en arrière.

## Conséquences
- « Interrompue » devient une issue de série, à côté de « gagnée » et « nulle ».
- Annuler n'a jamais à recalculer le cost, puisqu'aucune game terminée n'est effacée.
- Le départ d'un joueur peut aussi se gérer par un remplacement, sans arrêter la série.
- Le comportement actuel de `AbandonSeries` (suppression systématique) change.
- La correction d'une game par un Administrateur (vainqueur, composition) recalcule le score et l'issue de la série, mais ne modifie pas l'équipe de la série (ses joueurs) : l'équipe d'une série reste celle de son lancement.
