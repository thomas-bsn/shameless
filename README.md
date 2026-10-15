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

Au démarrage, le compose lance dans l'ordre :

1. **postgres** : la base, dans le volume nommé `postgres-data` ;
2. **migrate** : applique les migrations puis s'arrête ;
3. **api** : ne démarre qu'une fois la base saine et la migration réussie ;
4. **proxy** : Caddy, qui sert `/api` vers l'API et une page d'attente sur `/`.

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
| `ADMIN_DISCORD_IDS` | IDs Discord des admins (saisie des résultats, abandon de série, cost et peak). |
| `SESSION_LIFETIME_DAYS` | Durée de la session, prolongée à chaque utilisation (14 jours par défaut). |
| `RIOT_API_KEY` | Clé Riot, optionnelle : sans elle, le rafraîchissement des rangs est désactivé. |
| `SHAMELESS_SITE_ADDRESS` | `:80` en local, ou un nom de domaine pour le HTTPS automatique de Caddy. |

Aucun secret ni identifiant d'environnement n'est versionné : tout passe par `.env`, qui est ignoré par Git.

## Structure du monorepo

```
api/              Backend .NET 10 (Api / Application / Infrastructure)
  database/       Schéma legacy de référence et procédure de bascule (BASELINE.md)
  Shameless.Tests.Unit/         Tests du calcul MMR / cost
  Shameless.Tests.Integration/  Tests HTTP de bout en bout sur un vrai Postgres
deploy/caddy/     Reverse proxy et page d'attente
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

Le document OpenAPI est exposé en développement uniquement, sur `/openapi/v1.json`.

## CI

À chaque PR et push sur `main`, GitHub Actions restaure, compile et teste l'API, puis construit les images du compose.
