# Shameless — Roadmap

Source de vérité des phases. Une phase = une spec (grill-me → spec → plan → implémentation).
Quand une phase est finie : cocher ici, mettre à jour "Phase en cours" dans CLAUDE.md, tag git.

Fin du projet "v1.0" = V3 terminée. Tout ce qui vient après va dans "Backlog".

---

## Phase 0 — Nettoyage et monorepo
- [ ] Monorepo web/ api/ companion/ contracts/, licence MIT, README, .env.example (reste : companion/, avec l'app de la 1b)
- [x] Backend importé dans api/, passage .NET 10
- [x] Sécurité backend : [Authorize] sur games/series, expiration JWT corrigée, token hors de l'URL de callback, gestion globale des erreurs
- [x] OpenAPI + client TypeScript généré
- [x] CI GitHub Actions + premiers tests d'intégration (Testcontainers)
- [ ] docker-compose (api, web, postgres), déploiement NAS (Forgeyard si possible) (compose fait, déploiement NAS à confirmer)
- [x] Migration des données depuis le dump de lol_customs (migration Baseline à la place des migrations legacy, voir api/database/BASELINE.md)
- [x] Front Vite + React + TanStack + Tailwind + shadcn/ui, écrans existants repris sans styles inline

**Terminé quand** : `docker compose up` sert l'API et le front avec les données existantes, CI verte, endpoints protégés.

## Phase 1 — V1 : équipes, stats, historique
Objectif : une soirée complète sans saisie manuelle.

### 1a. Équipes et séries
- [x] Picker : Draft, Roulette, Autorole (nouveau)
- [x] Séries BO1, BO2, BO3, BO5 + option Fearless
- [x] État de la soirée sauvegardé côté API (plus de perte au refresh)
- [x] Draft de champions en ligne (capitaines et spectateurs, sans connexion)

### 1b. Companion (récupération des stats)
- [ ] App .NET 10 Avalonia, icône barre des tâches, login Discord
- [ ] Détection du client LoL via lockfile + écoute WebSocket locale
- [x] Rattachement de la custom à la série en cours (joueurs du lobby vs équipes)
- [ ] Envoi des stats de fin de partie des 10 joueurs à l'API (un seul joueur équipé suffit)
- [x] Dédoublonnage côté API ; saisie manuelle gardée en secours

### 1c. Stats et historique
- [x] RiotMatchData / RiotParticipantData remplis
- [x] Stats joueur : winrate, KDA, CS/min, KP, dégâts, champions, rôles
- [x] Fiche joueur : champions, rôle, perfs, Discord, cost, historique, rang SoloQ
- [x] Classements SoloQ et 5v5, cost mensuel conservé mois par mois (PlayerMonthlyCost)

### 1d. Bot Discord (repo séparé, Python)
- [x] Réécriture : un cog par projet, config env, base de données, Docker
- [x] Cog Shameless : détecte joueurs/specs en vocal, annonce la série, poste le récap en embed
- [x] Pré-remplit le Picker avec les joueurs en vocal (utilisateur lié à un joueur)
- [x] Déplace les équipes formées dans team-1 / team-2 ; les autres restent spectateurs
- [x] Communication par API HTTP authentifiée + webhooks

## Phase 2 — V2 : votes, spectateurs, clips
- [x] Votes de fin de game : MVP (Roi/Reine) et DOFD (Dick of the Day, image de bouse de cheval)
- [x] Votes en base (remplace /polls), badges sur fiche joueur et historique
- [x] Hall de la Honte (cumul DOFD) ~~+ mention Honte automatique par les stats~~
- [x] Spectateurs : pronostics avant la game (vainqueur, MVP, DOFD), ~~points, titre de Doyen~~
- [x] Clips : postés dans #clips, téléchargés par le bot, rattachés à la game

Votes, pronostics et clips : côté API et site livrés (#59), intégration Discord du bot
livrée dans Uchuu (#13, PR #23).

Hors périmètre de la Phase 2 (#59), plus tard s'ils sont validés : mention Honte automatique
par les stats ; points, classement des pronostics et titre de Doyen (voix double).

## Phase 3 — V3 : modes de jeu et replay
- [ ] Undercover : un traître par équipe, révélé par MP, vote final, classement dédié
- [ ] Mode Proxchat sur la série (proxchat reste un logiciel séparé)
- [ ] Replay 3D react-three-fiber : déplacements, kills, objectifs
- [ ] Positions : proxchat + Companion ; timeline match-v5 si clé Production obtenue

## Backlog
- Teaser vidéo avec vraies captures
- Fusion proxchat dans le Companion
