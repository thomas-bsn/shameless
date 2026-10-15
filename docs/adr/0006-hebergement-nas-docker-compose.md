# 0006 — Hébergement sur le NAS avec Docker Compose

Statut : acceptée — 2026-10-15

## Contexte
L'ancienne version tournait en conteneurs sur le NAS (lol_back, lol_front, lol_postgres).
Le NAS fait aussi tourner le PaaS Forgeyard.

## Décision
Shameless se déploie avec un `docker-compose.yml` (api, web, postgres) sur le NAS, via
Forgeyard si Forgeyard gère Postgres, les sauvegardes et le HTTPS. Sinon, Docker Compose
seul. Les secrets passent par un fichier `.env`, jamais commité ; `.env.example` est dans
le repo.

## Conséquences
- Une panne du NAS coupe le site : les sauvegardes Postgres sont automatiques et stockées
  hors du NAS.
- Toute autre communauté peut déployer sa propre instance avec le même compose.
