# Shameless

L'app des customs 5v5 entre potes : joueurs, games, séries en BO, et calcul du cost pour équilibrer les équipes. Connexion par Discord, réservée aux membres du serveur de la communauté.

## Lancer une instance en local

Prérequis : Docker avec Compose v2. Aucun fichier `.env` n'est nécessaire : un wizard d'installation collecte la configuration au premier démarrage.

```sh
docker compose up -d --build
docker compose logs wizard      # affiche le code d'accès à usage unique
```

Depuis un clone du dépôt, `compose.override.yaml` est chargé automatiquement : il construit les images depuis les sources et publie le proxy sur les ports 8080 et 8443. Ouvrir http://localhost:8080, saisir le code d'accès lu dans les logs, puis suivre les étapes : adresse publique, application Discord, options (clé Riot, fuseau horaire, intégration Uchuu : toutes facultatives), récapitulatif. À la confirmation, l'Instance démarre toute seule et le wizard s'arrête ; `http://localhost:8080/api/health` répond `Healthy`.

Le cookie de session est toujours marqué `Secure`. Chrome et Firefox l'acceptent sur `http://localhost`, Safari non : en local, utiliser l'un des deux, ou servir l'instance en HTTPS. L'adresse publique saisie dans le wizard doit être en HTTPS (seul un accès `http://localhost` est toléré).

### Comment se déroule le premier démarrage

Le Compose lance tous les services d'un coup, sans `.env` :

1. **wizard** : un serveur temporaire, joint via le proxy. Il est protégé par un code (16 caractères, à usage unique, valable 30 minutes) affiché dans ses logs. Il écrit la configuration dans le volume nommé `instance-config` (`instance.env`, puis le marqueur `ready`), attend que l'API réponde, puis s'arrête. Il ne se relance jamais sur une Instance configurée. Si le démarrage échoue, l'écran d'échec permet de corriger la saisie et de réessayer : la configuration corrigée est relue automatiquement par les autres services (sinon : `docker compose restart migrate api proxy`) ;
2. **postgres**, **migrate**, **api** : attendent le marqueur `ready`, puis démarrent dans l'ordre (base, migrations, API). Les migrations ne s'appliquent jamais au démarrage de l'API, seulement via le service `migrate` ;
3. **proxy** : Caddy ne sert que le wizard (HTTP sur le port 80, HTTPS auto-signé sur le port 443) tant que `ready` n'existe pas, puis redémarre avec l'adresse publique saisie (HTTPS automatique pour un domaine), `/api` vers l'API et le front sur tout le reste.

Si le code a expiré ou si trop d'essais ont échoué : `docker compose restart wizard` en fait émettre un nouveau. Si le démarrage échoue, le wizard l'explique (`docker compose logs migrate api` donne la cause) et permet de corriger la saisie.

Le volume `instance-config` contient des secrets en clair : le sauvegarder avec `postgres-data`, `api-keys` et `api-clips`. Voir l'[ADR 0015](docs/adr/0015-wizard-d-installation-docker-compose.md).

### Instance existante (avant le wizard)

Une Instance installée avec un `.env` peut garder sa base : créer `instance.env` et `ready` dans le volume `instance-config` avec les mêmes valeurs (mot de passe de base de `POSTGRES_PASSWORD`, `Auth__Discord__*`, `Auth__AllowedReturnOrigins`, `Auth__AdminDiscordIds`, `ConnectionStrings__Shameless`, `SHAMELESS_SITE_ADDRESS` ; Riot, fuseau et bot peuvent rester dans le `.env`), au format `CLE='valeur'`. Sans cela, le wizard s'ouvrirait et générerait un mot de passe de base différent de celui du volume `postgres-data`.

### Déployer avec les images publiées

Sans cloner le dépôt ni construire quoi que ce soit : récupérer `compose.yaml` et `compose.ports.yaml`, puis :

```sh
docker compose -f compose.yaml -f compose.ports.yaml up -d
docker compose -f compose.yaml -f compose.ports.yaml logs wizard
```

Le compose tire `ghcr.io/thomas-bsn/shameless-{api,migrate,proxy,wizard}` (`-f compose.yaml` évite l'override de dev, qui construit). `compose.ports.yaml` ne fait que publier le proxy sur la machine.

### Avec Forgeyard

`compose.yaml` est la base de tout : le coller tel quel dans un projet Forgeyard (Nouvelle app › Docker Compose) mène au même wizard, sans variable à renseigner ni fichier à modifier. Il se limite pour cela à ce que Forgeyard accepte (images publiées, volumes nommés, pas de `ports`, de `user` ni de `build`) ; ses extensions `x-forgeyard` (service exposé, sauvegarde `pg_dump` de la base) sont ignorées par Docker Compose. Le wizard s'ouvre sur l'adresse du projet : Forgeyard y termine le HTTPS, et le wizard le détecte pour que Caddy serve l'Instance en HTTP derrière lui, sans demander de certificat. `tests/compose/forgeyard-compat.mjs` vérifie ces contraintes. Les images ne contiennent aucun secret : la configuration vient du wizard (et, pour les réglages avancés, d'un `.env` facultatif, voir `.env.example`).

Pour un domaine en HTTPS automatique, Caddy doit être joignable sur les ports 80 et 443 : `SHAMELESS_HTTP_PORT=80` et `SHAMELESS_HTTPS_PORT=443`. Le wizard est servi sur ces mêmes ports (en HTTPS auto-signé sur le 443 : l'avertissement du navigateur est attendu) ; ne pas laisser un wizard non configuré exposé plus longtemps que nécessaire.

Tags publiés à chaque version `vX.Y.Z` (semver), pour les quatre images :

- `X.Y.Z` : version exacte, reproductible. Épingler `SHAMELESS_VERSION=X.Y.Z` (sans le `v`) pour une instance prévisible ;
- `X.Y` : dernier correctif de la série ;
- `latest` : tag stable, dernière version non préversion. C'est le défaut du compose.

Une préversion (`vX.Y.Z-rc.1`) ne pose que son tag exact. Les images sont multi-architecture (amd64, arm64). Les packages GHCR doivent être rendus publics dans les réglages GitHub pour être tirés sans authentification.

#### Publication automatique et manuelle

- **Parcours** : merge sur `main` => CI => publication automatique. Tout est dans un seul workflow, `.github/workflows/ci.yml` : les tests, puis le job de publication (push sur `main` ou lancement manuel). Il ne se déclenche pas sur un tag : le tag qu'il pose ne relance rien.
- **Automatique** : après chaque CI verte sur `main`, la version suivante est calculée à partir du dernier tag `vX.Y.Z` (les préversions sont ignorées ; sans tag, on part de `0.0.0`), le tag `vX.Y.Z` est posé sur le commit testé, puis les quatre images sont publiées dans le même run. Une CI en échec ne publie rien.
- **Niveau de bump** : patch par défaut. Le label `minor` ou `major` sur la PR mergée, ou `[minor]` / `[major]` dans son titre, passe au niveau supérieur (`major` l'emporte sur `minor`). Le label doit être posé avant le merge ; il se lit sur la PR du commit, donc un push direct sur `main` donne toujours un patch. Si deux merges se suivent de près, la publication en attente remplace la précédente : le label de la PR ignorée est alors perdu, la version suivante part du dernier tag.
- **Manuelle** (préversion, ou version précise) : onglet Actions de GitHub, workflow « CI », bouton **Run workflow** : choisir la branche, saisir la version sans le `v` (`0.2.0-rc.1` ou `0.2.0`). Les images sont publiées avec ce tag exact, et le tag `vX.Y.Z` est posé sur le commit de la branche choisie, sans calcul automatique. Une préversion ne pose ni `latest` ni `X.Y`. Un tag déjà posé sur un autre commit est refusé.
- Le calcul de version est dans `.github/scripts/prochaine-version.sh`, testé par `tests/deploy/prochaine-version.test.sh` (lancé dans la CI).

### Application Discord

Dans le [portail développeur Discord](https://discord.com/developers/applications), créer une application. Le wizard guide chaque étape et affiche l'URL de callback à déclarer dans **OAuth2 > Redirects** : `<adresse publique>/api/auth/discord/callback` (en local : `http://localhost:8080/api/auth/discord/callback`). Il demande :

- le Client ID et le Client Secret de l'application (le secret reste masqué et n'est jamais écrit dans les logs) ;
- l'ID du serveur Discord de la communauté (mode développeur, clic droit sur le serveur, « Copier l'identifiant ») : seuls ses membres peuvent se connecter ;
- une connexion avec votre propre compte Discord (bouton « Se connecter avec Discord ») : ce compte devient le premier Superadministrateur d'une installation neuve (sur une base qui a déjà un Superadministrateur, ce compte est ignoré : utilisez la commande `superadmin ajouter`, voir plus bas). Il faut aussi déclarer, dans **OAuth2 > Redirects**, une seconde URL affichée par le wizard : `<adresse publique>/_setup/api/discord/callback`, et ouvrir le wizard à cette adresse publique (en local, `http://localhost:8080`). Le wizard ne lit que l'identifiant du compte (scope `identify`), une seule fois, et ne garde pas le jeton. Un refus ou un échec est expliqué et la connexion peut être relancée.

### Options et bot Uchuu

L'étape « Options » du wizard est entièrement facultative : la clé Riot peut rester vide (rangs non rafraîchis), le fuseau vaut `Europe/Paris` par défaut, et l'intégration Uchuu (bot Discord) peut être ignorée. Si elle est activée, le wizard demande l'adresse publique du bot, génère trois secrets distincts (une fois par session de wizard, réutilisés si l'installation est relancée), les écrit dans `instance.env` et affiche à la fin :

- **Dans Shameless** : `SHAMELESS_BOT_URL`, `SHAMELESS_WEB_URL`, `SHAMELESS_BOT_API_KEY`, `SHAMELESS_WEBHOOK_SECRET`, `SHAMELESS_BOT_TO_API_KEY` (déjà enregistrées) ;
- **À reporter dans le bot** : les trois secrets (mêmes valeurs), `SHAMELESS_WEB_URL`, `SHAMELESS_GUILD_ID` (l'ID du serveur Discord saisi) et `SHAMELESS_API_URL` (`<adresse publique>/api` : le bot y ajoute `/bot/…` et le proxy retire le préfixe `/api` avant de joindre l'API ; à reporter avec `/api`), avec un bouton Copier par valeur et pour le bloc `.env` entier ;
- les variables du bot à saisir à la main (salons vocaux et textuels, `SHAMELESS_CLIPS_MAX_BYTES` aligné sur Shameless), décrites dans `docs/shameless.md` du dépôt `thomas-bsn/harceleur_bot`.

Le wizard n'appelle jamais le bot ni Discord pour cette étape et ne modifie rien d'autre. Ces valeurs ne sont affichées qu'une fois, mais restent relisibles après l'arrêt du wizard :

```sh
docker compose exec api cat /instance/instance.env
```

Contrat complet : [ADR 0016](docs/adr/0016-contrat-de-configuration-shameless-uchuu.md).

## Variables

Le wizard écrit lui-même la configuration de base (adresse, Discord, premier Superadministrateur, mot de passe de la base, et les options ci-dessus dans `instance.env`). Les autres réglages se posent dans un `.env` (voir [`.env.example`](.env.example)) ; les variables ci-dessous qui existent aussi dans `instance.env` (clé Riot, fuseau, bot) y sont prioritaires sur le `.env` quand elles ne sont pas vides :

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

## Rôles et Superadministrateur

Trois rôles sont stockés en base : utilisateur, Administrateur, Superadministrateur ([ADR 0017](docs/adr/0017-roles-et-superadministrateur.md)). Les identifiants de `Auth__AdminDiscordIds` (le Wizard y écrit le compte Discord connecté par l'Opérateur) ne servent qu'une fois : au premier démarrage qui suit l'arrivée des rôles, tant qu'aucun Superadministrateur n'existe, ils le deviennent. Ensuite la configuration n'est plus lue pour les droits. Les Administrateurs se nomment depuis le site ; les Superadministrateurs, uniquement par la commande de l'image de l'API :

```sh
docker compose exec api dotnet Shameless.Api.dll superadmin lister
docker compose exec api dotnet Shameless.Api.dll superadmin ajouter <identifiant Discord>
docker compose exec api dotnet Shameless.Api.dll superadmin retirer <identifiant Discord>
```

La commande relit la connexion à la base dans `/instance/instance.env` et ne démarre pas le site. Elle refuse de retirer le dernier Superadministrateur (code de sortie 1) ; un usage incorrect donne le code 2.

Aucun secret ni identifiant d'environnement n'est versionné : `.env` est ignoré par Git et la configuration du wizard vit dans un volume.

## Structure du monorepo

```
api/              Backend .NET 10 (Api / Application / Infrastructure)
  database/       Schéma legacy de référence et procédure de bascule (BASELINE.md)
  Shameless.Tests.Unit/         Tests du calcul MMR / cost
  Shameless.Tests.Integration/  Tests HTTP de bout en bout sur un vrai Postgres
web/              Front : SPA Vite + React + TanStack + Tailwind + shadcn/ui
contracts/        Document OpenAPI de l'API, source du client TypeScript du front
companion/        Companion : l'app Windows (.NET 10, Avalonia) qui envoie les stats de fin de game ; spike/ est un outil de capture jetable
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

### Sons de la draft de champions

Les sons de LoL sont des fichiers de Riot sous copyright : ils ne sont pas dans le dépôt (licence MIT). Pour les activer, héberger `debut.ogg`, `tour.ogg`, `verrouille.ogg` et `fin.ogg` à un emplacement de son choix et définir, au build du front, `VITE_DRAFT_SONS_URL` (ex. `VITE_DRAFT_SONS_URL=https://exemple.fr/sons npm run build`). Sans cette variable, la draft marche en silence et le bouton de son n'apparaît pas. Rien ne se joue avant une première action sur la page ; le choix « couper le son » est retenu sur l'appareil.

Les appels passent par `src/api/client.ts` : `$api` (hooks TanStack Query) et `fetchClient`, typés par le client généré. Les routes sont des fichiers dans `src/routes/` (TanStack Router) ; `src/routeTree.gen.ts` est régénéré par Vite et commité. Les composants shadcn/ui s'ajoutent avec `npx shadcn@latest add <composant>`.

## CI

À chaque PR et push sur `main`, GitHub Actions :

- restaure, compile et teste l'API, et vérifie que `contracts/openapi.json` est à jour ;
- vérifie que le client TypeScript commité correspond au contrat, puis lance lint, typecheck, tests et build du front ;
- vérifie le compose de déploiement (aucun `build:`, images GHCR) via `tests/deploy/verifier-publication.sh`, puis construit les images du compose ;
- type-check et teste le serveur du wizard (`wizard/`) ;
- compile et teste le Companion (`companion/`), et le script qui décide de publier son exécutable (`tests/deploy/companion-a-change.test.sh`) ;
- joue le test d'acceptation `tests/compose/acceptance.sh` (job `compose-acceptance`) : Compose vierge, wizard, site accessible, connexion Discord possible, redémarrage. Localement : `tests/compose/acceptance.sh` (Docker requis).

Après une CI verte sur `main`, `ci.yml` pose le tag `vX.Y.Z` suivant et publie les images dans GHCR ; une préversion ou une version précise se publie à la main avec le bouton **Run workflow** (voir « Publication automatique et manuelle »).

### Exécutable du Companion

Quand un push sur `main` change l'exécutable depuis la dernière Release (tout sous `companion/` sauf `spike/`, `tests/` et les `.md`, profil de publication compris, voir `.github/scripts/companion-a-change.sh`), `ci.yml` construit `shameless-companion.exe` (Windows 64 bits, un seul fichier, sans .NET à installer) et le publie en **GitHub Release** sur le tag `vX.Y.Z` du commit. Les autres pushs ne publient rien. Les options de construction sont dans `companion/src/Shameless.Companion.App/Properties/PublishProfiles/Windows.pubxml`. Le fichier a un nom fixe : `https://github.com/thomas-bsn/shameless/releases/latest/download/shameless-companion.exe` donne toujours le plus récent. Le dépôt ne crée aucune autre Release : sinon « latest » pourrait viser autre chose. Il n'est pas signé : Windows affiche un avertissement « éditeur inconnu » au premier lancement. Mise à jour automatique et signature restent à faire.
