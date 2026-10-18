#!/usr/bin/env bash
# Test d'acceptation de l'installation (#70, #71) à la frontière Docker Compose.
# Part d'un Compose vierge (aucun .env, aucun volume), suit le parcours de l'Opérateur par HTTP
# (code d'accès lu dans les logs, saisie, confirmation), puis vérifie que le site s'ouvre, que la
# connexion Discord est possible, que le wizard s'est arrêté, que rien de secret n'est dans les
# logs et que l'Instance repart seule, avec ses données, après un redémarrage.
# Deux parcours complets : toutes les options facultatives désactivées, puis Riot + fuseau + Uchuu ;
# un parcours derrière un proxy qui termine le HTTPS (Forgeyard), puis une reprise après échec : l'installation échoue, l'Opérateur corrige, la configuration corrigée est reprise.
#
# Usage : tests/compose/acceptance.sh        (Docker et Docker Compose v2 requis)
#   SKIP_BUILD=1  ne reconstruit pas les images locales (shameless-*:local, voir compose.override.yaml)
set -euo pipefail
# Pas de `set -x` : la trace afficherait le Client Secret et les clés dans les commandes curl.
cd "$(dirname "$0")/../.."

PORT=18080
BASE="http://localhost:$PORT"
SETUP="$BASE/_setup/api"
export SHAMELESS_HTTP_PORT=$PORT SHAMELESS_HTTPS_PORT=18443
export WIZARD_DISCORD_CHECK=off   # pas d'appel réseau vers Discord pendant le test
export WIZARD_INSTALL_TIMEOUT_SECONDS=240
unset SHAMELESS_VERSION SHAMELESS_IMAGE_PREFIX

# --env-file /dev/null : un .env local ne doit pas fausser un Compose « vierge ».
dc() { docker compose --env-file /dev/null -p shameless-acceptance "$@"; }

TMP=$(mktemp -d)
CLIENT_SECRET="ACCEPTANCE-secret-0123456789abcdef"
CLIENT_ID="123456789012345678"
GUILD_ID="987654321098765432"
ADMIN_ID="111111111111111111"
RIOT_KEY="RGAPI-acceptance-0123456789"
BOT_URL="https://bot.example.org"

cleanup() { dc down -v --remove-orphans >/dev/null 2>&1 || true; rm -rf "$TMP"; }
trap cleanup EXIT
fail() { echo "FAIL $*" >&2; echo "--- logs ---" >&2; dc logs --no-color 2>&1 | tail -80 >&2; exit 1; }
ok() { echo "ok   $*"; }

# post JSON : post <jar> <route> <json> -> écrit le corps dans $TMP/body, imprime le code HTTP
post() {
  curl -s -o "$TMP/body" -w '%{http_code}' -b "$1" -c "$1" -H 'content-type: application/json' \
    ${POST_HEADER:+-H "$POST_HEADER"} -X POST "$SETUP/$2" -d "$3"
}
expect_status() { # expect_status <attendu> <obtenu> <message>
  [ "$1" = "$2" ] || fail "$3 (attendu $1, obtenu $2 : $(cat "$TMP/body" 2>/dev/null))"
  ok "$3"
}
wait_for() { # wait_for <secondes> <description> <commande...>
  local max=$1 desc=$2; shift 2
  for _ in $(seq "$max"); do "$@" >/dev/null 2>&1 && { ok "$desc"; return 0; }; sleep 1; done
  fail "$desc (délai dépassé)"
}
api_healthy() { [ "$(curl -s "$BASE/api/health")" = "Healthy" ]; }
code_from_logs() { dc logs --no-color wizard 2>&1 | grep -o "Code d'accès : [A-Z0-9-]*" | tail -1 | sed 's/.*: //'; }
has_code() { [ -n "$(code_from_logs)" ]; }
wizard_stopped() { [ "$(dc ps -a --format '{{.State}}' wizard)" = "exited" ]; }

# La commande de relecture documentée sur l'écran de fin et dans le README.
instance_env() { dc exec -T api cat /instance/instance.env; }
env_value() { # env_value <NOM> : valeur d'instance.env (format CLE='valeur'), vide si absente
  instance_env | sed -n "s/^$1='\(.*\)'\$/\1/p"
}
# Variables d'environnement réellement vues par le processus de l'API (PID 1, après l'entrypoint).
api_process_env() { dc exec -T api sh -c 'tr "\0" "\n" < /proc/1/environ'; }
process_value() { api_process_env | sed -n "s/^$1=//p"; }

login_redirects_to_discord() {
  local h; h=$(curl -s -D - -o /dev/null "$BASE/api/auth/discord/login")
  echo "$h" | grep -qi '^location: https://discord.com/api/oauth2/authorize' &&
    echo "$h" | grep -q "client_id=$CLIENT_ID" &&
    echo "$h" | grep -q 'redirect_uri=http%3A%2F%2Flocalhost%3A'"$PORT"'%2Fapi%2Fauth%2Fdiscord%2Fcallback'
}

[ "${SKIP_BUILD:-}" = "1" ] || { echo "== Construction des images"; dc build >/dev/null; }
dc down -v --remove-orphans >/dev/null 2>&1 || true

# --- A. Un code expiré est refusé -------------------------------------------------------------
echo "== Code d'accès expiré"
JAR_E="$TMP/expired.jar"
WIZARD_CODE_TTL_SECONDS=2 dc up -d wizard proxy >/dev/null
wait_for 60 "le wizard affiche son code dans les logs" has_code
EXPIRED=$(code_from_logs)
sleep 4
expect_status 410 "$(post "$JAR_E" unlock "{\"code\":\"$EXPIRED\"}")" "un code de plus de 30 minutes (ici 2 s) est refusé"
dc down -v --remove-orphans >/dev/null 2>&1

# --- Parcours d'installation : parcours <nom> <json des options> ----------------------------------
# Rend la saisie complète : Discord (toujours obligatoire) + les options propres au parcours.
saisie() {
  printf '{"publicUrl":"%s","clientId":"%s","clientSecret":"%s","guildId":"%s","adminIds":"%s",%s}' \
    "$BASE" "$CLIENT_ID" "$CLIENT_SECRET" "$GUILD_ID" "$ADMIN_ID" "$1"
}

parcours() {
  local nom=$1 options=$2 jar_a="$TMP/$1-a.jar" jar_b="$TMP/$1-b.jar"
  echo "== Compose vierge ($nom)"
  dc up -d >/dev/null
  wait_for 60 "le wizard affiche son code dans les logs" has_code
  local code; code=$(code_from_logs)

  curl -s "$BASE/" | grep -q "Installation de Shameless" || fail "GET / ne sert pas le wizard"
  ok "GET / sert le wizard (aucune variable d'Instance n'a été fournie)"
  ! api_healthy || fail "l'API répond avant l'installation"
  ok "l'API n'est pas démarrée avant l'installation"

  expect_status 401 "$(post "$jar_a" install '{}')" "sans code, l'installation est refusée"
  expect_status 401 "$(post "$jar_a" unlock '{"code":"AAAA-AAAA-AAAA-AAAA"}')" "un mauvais code est refusé"
  expect_status 200 "$(post "$jar_a" unlock "{\"code\":\"$code\"}")" "le code affiché dans les logs ouvre une session"
  expect_status 401 "$(post "$jar_b" unlock "{\"code\":\"$code\"}")" "le code est à usage unique"

  local invalide="{\"publicUrl\":\"$BASE\",\"clientId\":\"abc\",\"clientSecret\":\"x\",\"guildId\":\"1\",\"adminIds\":\"\",\"uchuu\":true,\"botUrl\":\"\",\"riotKey\":\"pas-une-cle\"}"
  expect_status 422 "$(post "$jar_a" install "$invalide")" "une saisie invalide est expliquée et refusée"
  grep -q '"clientId"' "$TMP/body" || fail "l'erreur ne nomme pas le champ clientId"
  grep -q '"riotKey"' "$TMP/body" || fail "l'erreur ne nomme pas le champ riotKey"
  grep -q '"botUrl"' "$TMP/body" || fail "l'erreur ne nomme pas le champ botUrl"
  ! api_healthy || fail "l'API a démarré malgré l'erreur"

  echo "== Installation ($nom)"
  expect_status 202 "$(post "$jar_a" install "$(saisie "$options")")" "la saisie valide est acceptée"
  cp "$TMP/body" "$TMP/$nom-install.json"

  wait_for 240 "l'API démarre seule et répond Healthy via le proxy" api_healthy

  curl -s "$BASE/" | grep -q "<title>Shameless</title>" || fail "GET / ne sert pas le site"
  ok "GET / sert le site Shameless"
  login_redirects_to_discord || fail "la connexion Discord ne redirige pas vers Discord avec le bon callback"
  ok "la connexion Discord est possible : redirection vers Discord avec le callback calculé"

  wait_for 60 "le wizard s'arrête après la confirmation" wizard_stopped
  [ "$(dc ps -a --format '{{.ExitCode}}' wizard)" = "0" ] || fail "le wizard s'est arrêté en erreur"
  [ "$(curl -s -o /dev/null -w '%{http_code}' "$SETUP/state")" != "200" ] || fail "le wizard répond encore"
  ok "plus aucun wizard n'écoute"

  echo "== Configuration relue avec la commande documentée ($nom)"
  PG_PASSWORD=$(env_value POSTGRES_PASSWORD)
  [ -n "$PG_PASSWORD" ] || fail "le mot de passe de base généré est introuvable dans instance.env"
  ok "« docker compose exec api cat /instance/instance.env » relit la configuration ; un mot de passe de base a été généré"
  [ -n "$(env_value SHAMELESS_TIMEZONE)" ] || fail "SHAMELESS_TIMEZONE absent d'instance.env"

  echo "== Commande superadmin de l'image de l'API ($nom)"
  PREMIER_ADMIN=$(env_value Auth__AdminDiscordIds | cut -d, -f1)
  dc exec -T api dotnet Shameless.Api.dll superadmin lister 2>/dev/null | grep -q "^$PREMIER_ADMIN " ||
    fail "« superadmin lister » ne montre pas l'identifiant de la configuration ($PREMIER_ADMIN) comme Superadministrateur"
  ok "« docker compose exec api dotnet Shameless.Api.dll superadmin lister » montre le Superadministrateur importé de la configuration"
}

# Secrets à ne jamais voir dans les logs ; chaque parcours complète la liste.
SECRETS_A_CACHER=("$CLIENT_SECRET")

verifier_logs() {
  local logs; logs=$(dc logs --no-color 2>&1)
  local s
  for s in "${SECRETS_A_CACHER[@]}"; do
    [ -n "$s" ] || continue
    if grep -qF -- "$s" <<<"$logs"; then fail "un secret apparaît dans les logs d'un conteneur"; fi
  done
  ok "aucun secret saisi ou généré (${#SECRETS_A_CACHER[@]} valeurs) dans les logs de tous les conteneurs"
}

redemarrage() {
  echo "== Redémarrage"
  dc exec -T postgres psql -U shameless -d shameless -qc "CREATE TABLE acceptance_marker (v text); INSERT INTO acceptance_marker VALUES ('conservé')" >/dev/null || fail "écriture en base impossible"
  dc down >/dev/null 2>&1
  dc up -d >/dev/null
  wait_for 240 "l'Instance repart seule après redémarrage des conteneurs" api_healthy
  login_redirects_to_discord || fail "la connexion Discord ne fonctionne plus après redémarrage"
  [ "$(dc exec -T postgres psql -U shameless -d shameless -Atc "SELECT v FROM acceptance_marker")" = "conservé" ] || fail "les données de la base ont été perdues"
  ok "la configuration et les données de la base sont conservées"
  wait_for 60 "le wizard ne se relance pas sur une Instance configurée" wizard_stopped
  if dc logs --no-color wizard 2>&1 | grep -q "Code d'accès :"; then fail "un nouveau code d'accès a été émis"; fi
  dc logs --no-color wizard 2>&1 | grep -q "déjà configurée" || fail "le wizard n'a pas reconnu l'Instance configurée"
  ok "aucun nouveau code d'accès : le wizard ne se rouvre jamais"
}

# --- B. Toutes les options facultatives désactivées -------------------------------------------
parcours "sans-options" '"riotKey":"","timezone":"","uchuu":false,"botUrl":""'
grep -q '"uchuu"' "$TMP/sans-options-install.json" && fail "un récapitulatif Uchuu est renvoyé alors que l'intégration est ignorée"
[ "$(env_value RIOT_API_KEY)" = "" ] || fail "RIOT_API_KEY devrait être vide"
[ "$(env_value SHAMELESS_TIMEZONE)" = "Europe/Paris" ] || fail "le fuseau par défaut devrait être Europe/Paris"
[ -z "$(instance_env | grep -E '^SHAMELESS_(BOT|WEB|WEBHOOK)' || true)" ] || fail "des variables Uchuu sont écrites alors que l'intégration est ignorée"
[ -z "$(process_value Bot__Url)" ] && [ -z "$(process_value Bot__OutboundApiKey)" ] && [ -z "$(process_value Auth__Riot__ApiKey)" ] || fail "l'API a reçu des valeurs Riot ou bot"
[ "$(process_value Instance__TimeZone)" = "Europe/Paris" ] || fail "l'API n'a pas reçu le fuseau par défaut"
[ "$(curl -s -o /dev/null -w '%{http_code}' "$BASE/api/bot/games/1")" = "401" ] || fail "les routes /bot devraient répondre 401 sans intégration Uchuu"
ok "intégration ignorée : les routes /bot répondent 401"
ok "options désactivées : Riot vide, fuseau Europe/Paris, aucune variable Uchuu, API démarrée sans"
SECRETS_A_CACHER+=("$PG_PASSWORD")
verifier_logs
redemarrage
dc down -v --remove-orphans >/dev/null 2>&1

# --- C. Riot, fuseau et intégration Uchuu ------------------------------------------------------
SECRETS_A_CACHER=("$CLIENT_SECRET")
parcours "uchuu" "\"riotKey\":\"$RIOT_KEY\",\"timezone\":\"europe/brussels\",\"uchuu\":true,\"botUrl\":\"$BOT_URL\""

echo "== Variables Uchuu"
for nom in SHAMELESS_BOT_URL SHAMELESS_WEB_URL SHAMELESS_BOT_API_KEY SHAMELESS_WEBHOOK_SECRET SHAMELESS_BOT_TO_API_KEY RIOT_API_KEY SHAMELESS_TIMEZONE; do
  [ -n "$(env_value "$nom")" ] || fail "$nom absent d'instance.env"
done
ok "les sept variables sont écrites dans instance.env"
[ "$(env_value SHAMELESS_BOT_URL)" = "$BOT_URL" ] || fail "SHAMELESS_BOT_URL incorrecte"
[ "$(env_value SHAMELESS_WEB_URL)" = "$BASE" ] || fail "SHAMELESS_WEB_URL incorrecte"
[ "$(env_value RIOT_API_KEY)" = "$RIOT_KEY" ] || fail "RIOT_API_KEY incorrecte"
[ "$(env_value SHAMELESS_TIMEZONE)" = "Europe/Brussels" ] || fail "le fuseau n'est pas normalisé (Europe/Brussels)"
K1=$(env_value SHAMELESS_BOT_API_KEY); K2=$(env_value SHAMELESS_WEBHOOK_SECRET); K3=$(env_value SHAMELESS_BOT_TO_API_KEY)
for k in "$K1" "$K2" "$K3"; do [[ "$k" =~ ^[0-9a-f]{64}$ ]] || fail "un secret Uchuu n'est pas un hexadécimal de 32 octets"; done
[ "$K1" != "$K2" ] && [ "$K1" != "$K3" ] && [ "$K2" != "$K3" ] || fail "les secrets Uchuu ne sont pas distincts"
ok "trois secrets Uchuu distincts (32 octets hexadécimaux)"

[ "$(process_value Bot__Url)" = "$BOT_URL" ] || fail "l'API n'a pas reçu Bot__Url"
[ "$(process_value Bot__OutboundApiKey)" = "$K1" ] || fail "l'API n'a pas reçu SHAMELESS_BOT_API_KEY"
[ "$(process_value Bot__WebhookSecret)" = "$K2" ] || fail "l'API n'a pas reçu SHAMELESS_WEBHOOK_SECRET"
[ "$(process_value Bot__ApiKey)" = "$K3" ] || fail "l'API n'a pas reçu SHAMELESS_BOT_TO_API_KEY"
[ "$(process_value Bot__WebUrl)" = "$BASE" ] || fail "l'API n'a pas reçu SHAMELESS_WEB_URL"
[ "$(process_value Auth__Riot__ApiKey)" = "$RIOT_KEY" ] || fail "l'API n'a pas reçu la clé Riot"
[ "$(process_value Instance__TimeZone)" = "Europe/Brussels" ] || fail "l'API n'a pas reçu le fuseau"
ok "l'API démarre avec la clé Riot, le fuseau et les valeurs Uchuu"

# Le récapitulatif renvoyé à la confirmation reprend les mêmes valeurs, plus SHAMELESS_API_URL du bot.
for v in "$K1" "$K2" "$K3" "$BASE/api" "SHAMELESS_GUILD_ID" "$GUILD_ID" "SHAMELESS_TEAM1_VOICE_ID" "voice-candidates"; do
  grep -qF -- "$v" "$TMP/uchuu-install.json" || fail "le récapitulatif Uchuu ne contient pas « ${v:0:24} »"
done
ok "le récapitulatif Uchuu reprend les valeurs écrites, SHAMELESS_API_URL et la liste des variables à saisir"

SECRETS_A_CACHER+=("$PG_PASSWORD" "$K1" "$K2" "$K3" "$RIOT_KEY")
verifier_logs
redemarrage
[ "$(process_value Bot__ApiKey)" = "$K3" ] && [ "$(process_value Instance__TimeZone)" = "Europe/Brussels" ] || fail "les valeurs Uchuu ne sont pas conservées après redémarrage"
ok "les valeurs Uchuu sont conservées après redémarrage"
verifier_logs
dc down -v --remove-orphans >/dev/null 2>&1

# --- D. Échec de l'installation, correction, puis succès --------------------------------------
# L'API ne peut pas démarrer (base arrêtée) : le wizard signale l'échec. L'Opérateur corrige sa
# saisie (fuseau) et réessaie : la configuration réécrite doit être reprise sans action manuelle.
echo "== Reprise après échec"
JAR_D="$TMP/reprise.jar"
WIZARD_INSTALL_TIMEOUT_SECONDS=30 dc up -d >/dev/null
wait_for 60 "le wizard affiche son code dans les logs" has_code
expect_status 200 "$(post "$JAR_D" unlock "{\"code\":\"$(code_from_logs)\"}")" "le code ouvre une session"
dc stop postgres >/dev/null 2>&1
expect_status 202 "$(post "$JAR_D" install "$(saisie '"riotKey":"","timezone":"Europe/Paris","uchuu":false,"botUrl":""')")" "la première saisie est acceptée"
phase_echec() { curl -s -b "$JAR_D" "$SETUP/state" | grep -q '"phase":"failed"'; }
wait_for 90 "le wizard signale l'échec : l'API n'a pas démarré dans le délai" phase_echec
curl -s -b "$JAR_D" "$SETUP/state" | grep -q 'docker compose logs' || fail "l'échec n'indique pas où lire les journaux"
ok "le message d'échec indique les journaux à consulter"
! api_healthy || fail "l'API répond alors que la base est arrêtée"

echo "== Correction et nouvel essai"
dc start postgres >/dev/null 2>&1
pg_pret() { dc exec -T postgres pg_isready -U shameless -d shameless; }
wait_for 90 "la base redémarre" pg_pret
expect_status 202 "$(post "$JAR_D" install "$(saisie '"riotKey":"","timezone":"Europe/Brussels","uchuu":false,"botUrl":""')")" "la saisie corrigée est acceptée après l'échec"
wait_for 240 "l'API démarre seule avec la configuration corrigée" api_healthy
[ "$(process_value Instance__TimeZone)" = "Europe/Brussels" ] || fail "l'API n'a pas repris le fuseau corrigé"
ok "l'API a pris en compte la configuration corrigée (fuseau Europe/Brussels), sans commande manuelle"
wait_for 60 "le wizard s'arrête une fois l'installation réussie" wizard_stopped
[ "$(docker inspect -f '{{.HostConfig.RestartPolicy.Name}}' "$(dc ps -a -q migrate)")" = "on-failure" ] || fail "migrate ne se relance pas en cas d'échec"
ok "migrate est relancée en cas d'échec (elle relit instance.env à chaque démarrage)"
proxy_recharge() { dc logs --no-color proxy 2>&1 | grep -q "instance.env a changé"; }
wait_for 30 "le proxy a relu instance.env modifié par la correction" proxy_recharge
! dc logs --no-color proxy 2>&1 | grep -q "Rechargement impossible" || fail "le proxy n'a pas pu recharger sa configuration"

echo "== Une API qui plante repart avec la configuration en vigueur"
dc run --rm --no-deps -T --entrypoint sh wizard -c "sed -i \"s|^SHAMELESS_TIMEZONE=.*|SHAMELESS_TIMEZONE='Europe/Rome'|\" /instance/instance.env" >/dev/null 2>&1 || fail "modification d'instance.env impossible"
# Arrêt interne (SIGTERM au PID 1) : Docker applique la politique de redémarrage. `docker kill`
# compterait comme un arrêt manuel et empêcherait le redémarrage.
dc exec -T api sh -c 'kill -TERM 1' >/dev/null 2>&1 || true
wait_for 120 "l'API relancée par Docker répond de nouveau" api_healthy
[ "$(process_value Instance__TimeZone)" = "Europe/Rome" ] || fail "l'API relancée n'a pas relu instance.env"
ok "après un plantage, l'API relit instance.env au redémarrage (restart: unless-stopped)"

SECRETS_A_CACHER=("$CLIENT_SECRET" "$(env_value POSTGRES_PASSWORD)")
verifier_logs

dc down -v --remove-orphans >/dev/null 2>&1

# --- E. Derrière un proxy qui termine le HTTPS (Traefik de Forgeyard) ------------------------------
# Le même Compose : l'adresse publique est un domaine en HTTPS, mais la page arrive en HTTP sur le
# port 80 avec X-Forwarded-Proto: https. Caddy ne doit pas gérer le HTTPS (ni rediriger vers lui-même).
echo "== Proxy en amont qui termine le HTTPS"
JAR_E2="$TMP/amont.jar"
DOMAINE="shameless.example.org"
dc up -d >/dev/null
wait_for 60 "le wizard affiche son code dans les logs" has_code
expect_status 200 "$(post "$JAR_E2" unlock "{\"code\":\"$(code_from_logs)\"}")" "le code ouvre une session"
amont() { curl -s -o /dev/null -w '%{http_code}' -H "Host: $DOMAINE" -H 'X-Forwarded-Proto: https' "$BASE/api/health"; }
POST_HEADER='X-Forwarded-Proto: https' expect_status 202 "$(POST_HEADER='X-Forwarded-Proto: https' post "$JAR_E2" install "$(printf '{"publicUrl":"https://%s","clientId":"%s","clientSecret":"%s","guildId":"%s","adminIds":"%s","riotKey":"","timezone":"","uchuu":false,"botUrl":""}' "$DOMAINE" "$CLIENT_ID" "$CLIENT_SECRET" "$GUILD_ID" "$ADMIN_ID")")" "l'installation d'un domaine HTTPS est acceptée derrière un proxy"
grep -q 'proxy en amont' "$TMP/body" || fail "le wizard n'a pas signalé le HTTPS géré en amont"
amont_sain() { [ "$(amont)" = "200" ]; }
wait_for 240 "l'Instance répond en HTTP sur le port 80 sans redirection, pour le domaine public" amont_sain
[ "$(env_value SHAMELESS_SITE_ADDRESS)" = ":80" ] || fail "Caddy devrait servir :80 derrière un proxy qui termine le HTTPS"
ok "Caddy ne gère pas le HTTPS : SHAMELESS_SITE_ADDRESS=:80"
[ "$(env_value Auth__Discord__RedirectUri)" = "https://$DOMAINE/api/auth/discord/callback" ] || fail "le callback Discord devrait rester l'adresse publique HTTPS"
ok "le callback Discord reste l'adresse publique en HTTPS"
wait_for 60 "le wizard s'arrête" wizard_stopped
dc down -v --remove-orphans >/dev/null 2>&1

echo "Acceptation OK"
