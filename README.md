# Shameless

L'app des customs 5v5 entre potes : joueurs, games, séries en BO, et calcul du cost pour équilibrer les équipes. Connexion par Discord, réservée aux membres du serveur de la communauté.

## Lancer une instance en local

Prérequis : Docker avec Compose v2. Aucun fichier `.env` n'est nécessaire : un wizard d'installation collecte la configuration au premier démarrage.

```sh
docker compose up -d --build
docker compose logs wizard      # affiche le code d'accès à usage unique
```

Depuis un clone du dépôt, `compose.override.yaml` est chargé automatiquement et construit les images depuis les sources. Ouvrir http://localhost:8080, saisir le code d'accès lu dans les logs, puis suivre les étapes : adresse publique, application Discord, options (clé Riot, fuseau horaire, intégration Uchuu : toutes facultatives), récapitulatif. À la confirmation, l'Instance démarre toute seule et le wizard s'arrête ; `http://localhost:8080/api/health` répond `Healthy`.

Le cookie de session est toujours marqué `Secure`. Chrome et Firefox l'acceptent sur `http://localhost`, Safari non : en local, utiliser l'un des deux, ou servir l'instance en HTTPS. L'adresse publique saisie dans le wizard doit être en HTTPS (seul un accès `http://localhost` est toléré).

### Comment se déroule le premier démarrage

Le Compose lance tous les services d'un coup, sans `.env` :

1. **wizard** : un serveur temporaire, joint via le proxy. Il est protégé par un code (16 caractères, à usage unique, valable 30 minutes) affiché dans ses logs. Il écrit la configuration dans le volume nommé `instance-config` (`instance.env`, puis le marqueur `ready`), attend que l'API réponde, puis s'arrête. Il ne se relance jamais sur une Instance configurée ;
2. **postgres**, **migrate**, **api** : attendent le marqueur `ready`, puis démarrent dans l'ordre (base, migrations, API). Les migrations ne s'appliquent jamais au démarrage de l'API, seulement via le service `migrate` ;
3. **proxy** : Caddy ne sert que le wizard (HTTP sur le port 80, HTTPS auto-signé sur le port 443) tant que `ready` n'existe pas, puis redémarre avec l'adresse publique saisie (HTTPS automatique pour un domaine), `/api` vers l'API et le front sur tout le reste.

Si le code a expiré ou si trop d'essais ont échoué : `docker compose restart wizard` en fait émettre un nouveau. Si le démarrage échoue, le wizard l'explique (`docker compose logs migrate api` donne la cause) et permet de corriger la saisie.

Le volume `instance-config` contient des secrets en clair : le sauvegarder avec `postgres-data`, `api-keys` et `api-clips`. Voir l'[ADR 0015](docs/adr/0015-wizard-d-installation-docker-compose.md).

### Instance existante (avant le wizard)

Une Instance installée avec un `.env` peut garder sa base : créer `instance.env` et `ready` dans le volume `instance-config` avec les mêmes valeurs (mot de passe de base de `POSTGRES_PASSWORD`, `Auth__Discord__*`, `Auth__AllowedReturnOrigins`, `Auth__AdminDiscordIds`, `ConnectionStrings__Shameless`, `SHAMELESS_SITE_ADDRESS` ; Riot, fuseau et bot peuvent rester dans le `.env`), au format `CLE='valeur'`. Sans cela, le wizard s'ouvrirait et générerait un mot de passe de base différent de celui du volume `postgres-data`.

### Déployer avec les images publiées

Sans cloner le dépôt ni construire quoi que ce soit : récupérer `compose.yaml`, puis :

```sh
docker compose -f compose.yaml up -d
docker compose -f compose.yaml logs wizard
```

Le compose tire `ghcr.io/thomas-bsn/shameless-{api,migrate,proxy,wizard}` (`-f compose.yaml` évite l'override de dev, qui construit). Les images ne contiennent aucun secret : la configuration vient du wizard (et, pour les réglages avancés, d'un `.env` facultatif, voir `.env.example`).

Pour un domaine en HTTPS automatique, Caddy doit être joignable sur les ports 80 et 443 : `SHAMELESS_HTTP_PORT=80` et `SHAMELESS_HTTPS_PORT=443`. Le wizard est servi sur ces mêmes ports (en HTTPS auto-signé sur le 443 : l'avertissement du navigateur est attendu) ; ne pas laisser un wizard non configuré exposé plus longtemps que nécessaire.

Tags publiés à chaque tag Git `vX.Y.Z` (semver), pour les quatre images :

- `X.Y.Z` : version exacte, reproductible. Épingler `SHAMELESS_VERSION=X.Y.Z` (sans le `v`) pour une instance prévisible ;
- `X.Y` : dernier correctif de la série ;
- `latest` : tag stable, dernière version non préversion. C'est le défaut du compose.

Une préversion (`vX.Y.Z-rc.1`) ne pose que son tag exact. Les images sont multi-architecture (amd64, arm64). Le workflow est `.github/workflows/release.yml` ; pour publier : `git tag vX.Y.Z && git push origin vX.Y.Z`. Les packages GHCR doivent être rendus publics dans les réglages GitHub pour être tirés sans authentification.

### Application Discord

Dans le [portail développeur Discord](https://discord.com/developers/applications), créer une application. Le wizard guide chaque étape et affiche l'URL de callback à déclarer dans **OAuth2 > Redirects** : `<adresse publique>/api/auth/discord/callback` (en local : `http://localhost:8080/api/auth/discord/callback`). Il demande :

- le Client ID et le Client Secret de l'application (le secret reste masqué et n'est jamais écrit dans les logs) ;
- l'ID du serveur Discord de la communauté (mode développeur, clic droit sur le serveur, « Copier l'identifiant ») : seuls ses membres peuvent se connecter ;
- au moins un ID Discord d'administrateur.

### Options et bot Uchuu

L'étape « Options » du wizard est entièrement facultative : la clé Riot peut rester vide (rangs non rafraîchis), le fuseau vaut `Europe/Paris` par défaut, et l'intégration Uchuu (bot Discord) peut être ignorée. Si elle est activée, le wizard demande l'adresse publique du bot, génère trois secrets distincts (une fois par session de wizard, réutilisés si l'installation est relancée), les écrit dans `instance.env` et affiche à la fin :

- **Dans Shameless** : `SHAMELESS_BOT_URL`, `SHAMELESS_WEB_URL`, `SHAMELESS_BOT_API_KEY`, `SHAMELESS_WEBHOOK_SECRET`, `SHAMELESS_BOT_TO_API_KEY` (déjà enregistrées) ;
- **À reporter dans le bot** : les trois secrets (mêmes valeurs), `SHAMELESS_WEB_URL`, `SHAMELESS_GUILD_ID` (l'ID du serveur Discord saisi) et `SHAMELESS_API_URL` (`<adresse publique>/api`), avec un bouton Copier par valeur et pour le bloc `.env` entier ;
- les variables du bot à saisir à la main (salons vocaux et textuels, `SHAMELESS_CLIPS_MAX_BYTES` aligné sur Shameless), décrites dans `docs/shameless.md` du dépôt `thomas-bsn/harceleur_bot`.

Le wizard n'appelle jamais le bot ni Discord pour cette étape et ne modifie rien d'autre. Ces valeurs ne sont affichées qu'une fois, mais restent relisibles après l'arrêt du wizard :

```sh
docker compose exec api cat /instance/instance.env
```

Contrat complet : [ADR 0016](docs/adr/0016-contrat-de-configuration-shameless-uchuu.md).

## Variables

Le wizard écrit lui-même la configuration de base (adresse, Discord, administrateurs, mot de passe de la base, et les options ci-dessus dans `instance.env`). Les autres réglages se posent dans un `.env` (voir [`.env.example`](.env.example)) ; les variables ci-dessous qui existent aussi dans `instance.env` (clé Riot, fuseau, bot) y sont prioritaires sur le `.env` quand elles ne sont pas vides :

| Variable | Rôle |
|---|---|
| `SESSION_LIFETIME_DAYS` | Durée de la session, prolongée à chaque utilisation (14 jours par défaut). |
| `RIOT_API_KEY` | Clé Riot, optionnelle : sans elle, le rafraîchissement des rangs est désactivé. |
| `SOIREE_AUTO_CLOSE_AFTER` | Période sans game au bout de laquelle la Soirée ouverte se ferme seule, au format `hh:mm:ss` (3 h par défaut). |
| `SHAMELESS_BOT_URL`, `SHAMELESS_BOT_API_KEY`, `SHAMELESS_WEBHOOK_SECRET` | Bot Discord, optionnel (ADR 0013) : URL du bot, clé API → bot (candidats vocaux) et secret HMAC des webhooks, mêmes valeurs que dans l'env du bot. |
| `SHAMELESS_BOT_TO_API_KEY` | Clé du bot pour les routes `/bot` de l'API, distincte des précédentes. Vide : routes refusées. |
| `SHAMELESS_WEB_URL` | URL publique du site, pour les liens envoyés au bot (récap de Game). |
| `SHAMELESS_TIMEZONE` | Fuseau horaire IANA de l'Instance (`Europe/Paris` par défaut) : il découpe les jours, ex. pour rattacher un clip à la Game du jour (ADR 0014). |
| `SHAMELESS_CLIPS_MAX_BYTES` | Taille maximale d'une vidéo de clip, en octets (500 Mo par défaut). Les vidéos vivent dans le volume nommé `api-clips`. |
| `SHAMELESS_HTTP_PORT`, `SHAMELESS_HTTPS_PORT` | Ports publiés par Caddy (8080 et 8443 par défaut ; 80 et 443 pour un domaine). |
| `WIZARD_CODE_TTL_SECONDS`, `WIZARD_INSTALL_TIMEOUT_SECONDS` | Validité du code d'accès (1800) et délai de démarrage de l'API avant d'annoncer un échec (300). |

Aucun secret ni identifiant d'environnement n'est versionné : `.env` est ignoré par Git et la configuration du wizard vit dans un volume.

## Structure du monorepo

```
api/              Backend .NET 10 (Api / Application / Infrastructure)
  database/       Schéma legacy de référence et procédure de bascule (BASELINE.md)
  Shameless.Tests.Unit/         Tests du calcul MMR / cost
  Shameless.Tests.Integration/  Tests HTTP de bout en bout sur un vrai Postgres
web/              Front : SPA Vite + React + TanStack + Tailwind + shadcn/ui
contracts/        Document OpenAPI de l'API, source du client TypeScript du front
wizard/           Assistant d'installation : serveur Node sans dépendance ; son front est dans web/src/wizard
deploy/caddy/     Reverse proxy, qui construit et sert le front
compose.yaml      Instance complète (images et volumes nommés uniquement)
docs/agents/      Configuration des skills d'agents (tracker, labels)
```

## Développer sur l'API

Prérequis : SDK .NET 10 et Docker (les tests d'intégration démarrent un Postgres jetable via Testcontainers).

```sh
cd api
dotnet build
dotnet test
```

Les outils EF sont déclarés dans le manifeste local : `dotnet tool restore`, puis par exemple `dotnet ef migrations add <Nom> -p Shameless.Infrastructure -s Shameless.Infrastructure`.

Le document OpenAPI est exposé en développement uniquement, sur `/openapi/v1.json`, sans session. Sa version commitée, `contracts/openapi.json`, est vérifiée par les tests d'intégration (`ContractTests`). Après un changement de contrat :

```sh
cd api
UPDATE_CONTRACTS=1 dotnet test --project Shameless.Tests.Integration --filter-class "*ContractTests"
cd ../web
npm run gen:api
```

## Développer sur le front

Prérequis : Node 24.

```sh
cd web
npm ci
npm run dev
```

Le front tourne sur http://localhost:5173 et relaie `/api` vers l'API lancée avec `dotnet run` (`http://localhost:5221` par défaut, `SHAMELESS_API_URL` pour changer de cible). Pour se connecter en local, déclarer `http://localhost:5173/api/auth/discord/callback` dans les Redirects de l'application Discord et le mettre dans `Auth:Discord:RedirectUri`.

| Commande | Rôle |
|---|---|
| `npm run gen:api` | Régénère le client TypeScript (`src/api/schema.ts`) depuis `contracts/openapi.json`. |
| `npm run lint` | ESLint, qui interdit aussi les styles inline (ADR 0002). |
| `npm run typecheck` | Vérification des types. |
| `npm test` | Tests Vitest. |
| `npm run build` | Build de production dans `dist/`. |

Les appels passent par `src/api/client.ts` : `$api` (hooks TanStack Query) et `fetchClient`, typés par le client généré. Les routes sont des fichiers dans `src/routes/` (TanStack Router) ; `src/routeTree.gen.ts` est régénéré par Vite et commité. Les composants shadcn/ui s'ajoutent avec `npx shadcn@latest add <composant>`.

## CI

À chaque PR et push sur `main`, GitHub Actions :

- restaure, compile et teste l'API, et vérifie que `contracts/openapi.json` est à jour ;
- vérifie que le client TypeScript commité correspond au contrat, puis lance lint, typecheck, tests et build du front ;
- vérifie le compose de déploiement (aucun `build:`, images GHCR) via `tests/deploy/verifier-publication.sh`, puis construit les images du compose ;
- type-check et teste le serveur du wizard (`wizard/`) ;
- joue le test d'acceptation `tests/compose/acceptance.sh` (job `compose-acceptance`) : Compose vierge, wizard, site accessible, connexion Discord possible, redémarrage. Localement : `tests/compose/acceptance.sh` (Docker requis).

Sur un tag `vX.Y.Z`, le workflow `release.yml` publie les images dans GHCR.
