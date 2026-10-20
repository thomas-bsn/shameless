# 0023 — Suppression du titre voté Roi/Reine

Statut : acceptée — 2026-10-20 (remplace en partie l'ADR 0013)

## Contexte
Les votes de fin de game (ADR 0013, Phase 2) portaient sur deux titres : le meilleur joueur de la
game, affiché « Roi/Reine » (nommé `mvp` dans le code et l'API), et le DOFD. Le badge « MVP » de la
page d'une game, lui, vient des notes (meilleure note de l'Équipe gagnante, `MeilleurePerf`) : les deux
se faisaient concurrence à l'écran. Le product owner n'a plus besoin de l'idée de Roi ou de Reine.

## Décision
- Le titre voté Roi/Reine disparaît partout : bulletin de vote, résultats de clôture, pronostics,
  compteurs de titres de la fiche du joueur, historique, badge du scoreboard, tutoriel, glossaire.
  Les votes ne portent plus que sur le DOFD, un joueur au plus par bulletin.
- Le pronostic de Roi/Reine, qui dépendait du résultat de ce vote, est supprimé avec lui ; restent le
  vainqueur et le DOFD.
- Les badges MVP (`MeilleurePerf`) et ACE, calculés par les stats, et le vote DOFD ne changent pas.
- Une migration EF supprime les colonnes `mvp_player_id` de `bulletins_de_vote` et de `pronostics`.
  Les votes et pronostics déjà enregistrés pour ce titre sont perdus ; un bulletin ou un pronostic qui ne
  portait que lui est supprimé. Les `down` recréent les colonnes vides, sans restaurer les données.
- Le contrat de l'API change (champ `mvpPlayerId` retiré des requêtes du bot, `mvp` retiré des
  réponses) : le bot Discord (repo séparé) doit cesser de proposer et d'envoyer le choix Roi/Reine.

## Conséquences
- L'ADR 0013 reste vrai pour le reste (identités machine, présences, DOFD, vainqueur) ; ses passages sur
  le MVP voté sont historiques.
- Rétablir un titre voté demanderait une nouvelle décision et de nouvelles colonnes.
