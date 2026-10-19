# Shameless — Roadmap

Source de vérité des phases. Une phase = une spec (grill-me → spec → plan → implémentation).
Quand une phase est finie : cocher ici, tag git.

Fin du projet "v1.0" = V3 terminée. Tout ce qui vient après va dans "Backlog".

---

## Phase 0 — Nettoyage et monorepo
- [ ] Monorepo web/ api/ companion/ contracts/, licence MIT, README, .env.example (reste : companion/, avec l'app de la 1b)
- [x] Backend importé dans api/, passage .NET 10
- [x] Sécurité backend : [Authorize] sur games/series, expiration JWT corrigée, token hors de l'URL de callback, gestion globale des erreurs
- [x] OpenAPI + client TypeScript généré
- [x] CI GitHub Actions + premiers tests d'intégration (Testcontainers)
- [x] docker-compose (api, web, postgres), déploiement NAS (Forgeyard si possible) (compose fait, déploiement NAS à confirmer)
- [x] Images publiées sur GHCR à chaque tag `vX.Y.Z` (api, migrate, proxy, wizard) (#69) ; un seul workflow `ci.yml` (auto après les tests, ou bouton Run workflow)
- [x] Publication automatique des images à chaque commit sur `main` (CI verte), avec bump de version et tag `vX.Y.Z` (#107)
- [x] Wizard d'installation Docker Compose : Discord, options, bot facultatif, un seul `compose.yaml` pour Docker Compose et Forgeyard (#70, #71, #72)
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
- [ ] Spec : Companion, l'app PC (#157) ; Windows uniquement, packaging en Backlog
- [ ] Spike : capturer les données de fin de game du client LoL sur un vrai poste (#158)
- [ ] Contrat API et site : champion en id numérique, route des administrateurs, Import d'un Rapport (#159, ADR 0019 et 0020)
- [ ] App .NET 10 Avalonia, icône barre des tâches, saisie et conservation du Code Companion (#160)
- [ ] Détection du client LoL via lockfile + écoute WebSocket locale (#161)
- [ ] Administration : Companion connecté, signal de présence du Companion (#146)
- [ ] Publication de l'exécutable Windows en GitHub Release par la CI (#164)
- [ ] Écran de préparation : adresse, Code et téléchargement du Companion ; tutoriel à jour (#165)
- [x] Rattachement de la custom à la série en cours (joueurs du lobby vs équipes)
- [ ] Envoi des stats de fin de partie des 10 joueurs à l'API (un seul joueur équipé suffit), file de renvoi et procédure d'échec avec le Fichier de Rapport (#162)
- [x] Dédoublonnage côté API ; saisie manuelle gardée en secours
- [ ] Signal de fin de partie (Rapport), sans retour sur le site ; le début est donné par la fin de la draft (ADR 0017)
- [ ] Side détecté par le Companion ; joueur inconnu signalé à l'organisateur dans le Rapport en revue

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

## Refonte du front (hors phases 0 à 3)
- [x] Refonte du front : thème sombre natif shadcn, sidebar, Fond vidéo, pages redessinées (spec #104)
  - [x] Socle : thèmes, Geist embarquée, sidebar et tiroir mobile (#85)
  - [x] Fond : les clips de #clips en arrière-plan (#86)
  - [x] Clips : onglet en fil par soirée et vue « Tous » filtrable (#87)
  - [x] Masquer et réafficher un clip du Fond, Administrateur (#88)
  - [x] Soirée : en cours, aucune soirée, soirée fermée (#89)
  - [x] Picker : joueurs, Roulette, équipes, autorole, série (#90)
  - [x] Picker : la Draft avec ses fenêtres (#91)
  - [x] Joueurs : tableau et ajout d'un joueur (#93)
  - [x] Joueurs : la fiche d'un joueur (#94)
  - [x] Classements : vues Cost et SoloQ (#95)
  - [x] Classements : vue DOFD et retrait du Hall de la Honte (#96)
  - [x] Historique : le calendrier du mois (#97)
  - [x] Page d'une game : scoreboard, votes, pronostics, clips (#98)
  - [x] Page d'une game : onglets Dégâts, Objectifs, Vision, Replay (#99)
  - [x] Administration : à traiter, cost et peak (#100)
  - [x] Administration : l'onglet Clips du Fond (#101)
  - [x] Capitaines, liens et draft de champions, avec le filtre de voie via Meraki (#105)
  - [ ] Parcours de série : Écran de préparation, Page de la Série allégée, Espace organisateur
  - [ ] Capitaines : jamais le même joueur des deux côtés ; liens envoyés en DM par le bot, « rejoint » mémorisé
  - [x] Game créée au lancement de la draft, `game_started` au signal du Companion, Side sans choix manuel

## Backlog
- Teaser vidéo avec vraies captures
- Fusion proxchat dans le Companion
- Companion : mise à jour automatique, signature de l'exécutable, installeur
- Administrateurs repérables dans Discord (pseudo en rouge) pour que le Companion dise « donne le fichier à un admin »
- Nom du bot lu depuis Forgeyard (aujourd'hui saisi dans le wizard)
- Wizard : générer ou expliquer `INTERNAL_API_KEY` du bot
- Forgeyard : suivre l'issue thomas-bsn/forgeyard#3 (compatibilité Compose, `user` et `ports`)
