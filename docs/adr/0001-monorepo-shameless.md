# 0001 — Un monorepo pour Shameless, des repos séparés pour le bot et le proxchat

Statut : acceptée — 2026-10-15

## Contexte
Le front (thomas-lol-custom-5v5) et le backend (thomas-lol-custom-5v5-backend) vivaient dans
deux repos. Le projet ajoute une app PC (Companion) et des types partagés entre l'API et le
Companion. Le bot Discord sert plusieurs projets, et le proxchat fonctionne sans Shameless.

## Décision
Un repo `shameless` contient `web/`, `api/`, `companion/` et `contracts/`.
Le bot Discord et le proxchat restent dans leurs propres repos. Ils parlent à Shameless
uniquement par son API HTTP et par des webhooks.

## Conséquences
- Une PR peut changer l'API et le front ensemble, et une seule CI vérifie les deux.
- Deux technos dans le même repo (.NET et Node) : la CI et le docker-compose gèrent les deux.
- Shameless ne dépend du code ni du bot ni du proxchat.
