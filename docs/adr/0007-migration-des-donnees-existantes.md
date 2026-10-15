# 0007 — Migrer les données existantes

Statut : acceptée — 2026-10-15

## Contexte
La base `lol_customs` contient les joueurs, les utilisateurs Discord, les séries,
l'historique et les cost de l'ancienne version.

## Décision
Shameless repart de cette base. Le schéma EF et ses 7 migrations sont conservés :
Shameless ajoute des migrations, il ne recrée pas la base. Les données sont importées depuis
un dump `pg_dump` de `lol_customs`.

## Conséquences
- Pas de perte d'historique pour les joueurs.
- Les choix de l'ancien schéma restent tant qu'une migration ne les change pas.
- Le dump contient des données personnelles : il n'est jamais commité.
