# Shameless

L'app des customs 5v5 entre potes : joueurs, games, séries en BO, et calcul du cost pour équilibrer les équipes. Connexion par Discord, réservée aux membres du serveur de la communauté.

## Lancer une instance en local

Prérequis : Docker avec Compose v2.

```sh
cp .env.example .env
# remplir au minimum POSTGRES_PASSWORD et les variables DISCORD_*
docker compose up -d --build
```

Puis ouvrir http://localhost:8080. L'API est servie sous `/api` ; `http://localhost:8080/api/health` doit répondre `Healthy`.

Le cookie de session est toujours marqué `Secure`. Chrome et Firefox l'acceptent sur `http://localhost`, Safari non : en local, utiliser l'un des deux, ou servir l'instance en HTTPS. Sur un vrai domaine, renseigner `SHAMELESS_SITE_ADDRESS` pour que Caddy active le HTTPS.

Au démarrage, le compose lance dans l'ordre :

1. **postgres** : la base, dans le volume nommé `postgres-data` ;
2. **migrate** : applique les migrations puis s'arrête ;
3. **api** : ne démarre qu'une fois la base saine et la migration réussie ;
4. **proxy** : Caddy, qui sert `/api` vers l'API et le front (build Vite, construit dans l'image) sur tout le reste.

Les migrations ne s'appliquent jamais au démarrage de l'API, seulement via le service `migrate`.

### Application Discord

Dans le [portail développeur Discord](https://discord.com/developers/applications), créer une application, puis dans **OAuth2** :

- copier le Client ID et le Client Secret dans `DISCORD_CLIENT_ID` et `DISCORD_CLIENT_SECRET` ;
- ajouter la valeur de `DISCORD_REDIRECT_URI` dans les Redirects (en local : `http://localhost:8080/api/auth/discord/callback`).

`DISCORD_GUILD_ID` est l'ID du serveur de la communauté (mode développeur Discord, clic droit sur le serveur, « Copier l'identifiant »).

## Variables

Toutes les variables sont décrites dans [`.env.example`](.env.example). Les principales :

| Variable | Rôle |
|---|---|
| `POSTGRES_PASSWORD` | Mot de passe de la base (requis). |
| `DISCORD_CLIENT_ID`, `DISCORD_CLIENT_SECRET` | Application OAuth2 Discord (requis). |
| `DISCORD_GUILD_ID` | Serveur dont les membres peuvent se connecter (requis). |
| `DISCORD_REDIRECT_URI` | URL de callback OAuth2, sous `/api` (requis). |
| `AUTH_ALLOWED_RETURN_ORIGINS` | Origines autorisées comme URL de retour après connexion. |
| `ADMIN_DISCORD_IDS` | IDs Discord des admins (cost et peak). |
| `SESSION_LIFETIME_DAYS` | Durée de la session, prolongée à chaque utilisation (14 jours par défaut). |
| `RIOT_API_KEY` | Clé Riot, optionnelle : sans elle, le rafraîchissement des rangs est désactivé. |
| `SOIREE_AUTO_CLOSE_AFTER` | Période sans game au bout de laquelle la Soirée ouverte se ferme seule, au format `hh:mm:ss` (3 h par défaut). |
| `SHAMELESS_SITE_ADDRESS` | `:80` en local, ou un nom de domaine pour le HTTPS automatique de Caddy. |

Aucun secret ni identifiant d'environnement n'est versionné : tout passe par `.env`, qui est ignoré par Git.

## Structure du monorepo

```
api/              Backend .NET 10 (Api / Application / Infrastructure)
  database/       Schéma legacy de référence et procédure de bascule (BASELINE.md)
  Shameless.Tests.Unit/         Tests du calcul MMR / cost
  Shameless.Tests.Integration/  Tests HTTP de bout en bout sur un vrai Postgres
web/              Front : SPA Vite + React + TanStack + Tailwind + shadcn/ui
contracts/        Document OpenAPI de l'API, source du client TypeScript du front
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
- construit les images du compose.
