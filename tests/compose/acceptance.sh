#!/usr/bin/env bash
# Test d'acceptation de l'installation (#70) à la frontière Docker Compose.
# Part d'un Compose vierge (aucun .env, aucun volume), suit le parcours de l'Opérateur par HTTP
# (code d'accès lu dans les logs, saisie, confirmation), puis vérifie que le site s'ouvre, que la
# connexion Discord est possible, que le wizard s'est arrêté, que rien de secret n'est dans les
# logs et que l'Instance repart seule, avec ses données, après un redémarrage.
#
# Usage : tests/compose/acceptance.sh        (Docker et Docker Compose v2 requis)
#   SKIP_BUILD=1  ne reconstruit pas les images locales (shameless-*:local, voir compose.override.yaml)
set -euo pipefail
[ -z "${DEBUG:-}" ] || set -x
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
JAR_A="$TMP/a.jar"; JAR_B="$TMP/b.jar"
CLIENT_SECRET="ACCEPTANCE-secret-0123456789abcdef"
CLIENT_ID="123456789012345678"
GUILD_ID="987654321098765432"
ADMIN_ID="111111111111111111"

cleanup() { dc down -v --remove-orphans >/dev/null 2>&1 || true; rm -rf "$TMP"; }
trap cleanup EXIT
fail() { echo "FAIL $*" >&2; echo "--- logs ---" >&2; dc logs --no-color 2>&1 | tail -80 >&2; exit 1; }
ok() { echo "ok   $*"; }

# post JSON : post <jar> <route> <json> -> écrit le corps dans $TMP/body, imprime le code HTTP
post() {
  curl -s -o "$TMP/body" -w '%{http_code}' -b "$1" -c "$1" -H 'content-type: application/json' \
    -X POST "$SETUP/$2" -d "$3"
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

[ "${SKIP_BUILD:-}" = "1" ] || { echo "== Construction des images"; dc build >/dev/null; }
dc down -v --remove-orphans >/dev/null 2>&1 || true

# --- A. Un code expiré est refusé -------------------------------------------------------------
echo "== Code d'accès expiré"
WIZARD_CODE_TTL_SECONDS=2 dc up -d wizard proxy >/dev/null
wait_for 60 "le wizard affiche son code dans les logs" has_code
EXPIRED=$(code_from_logs)
sleep 4
expect_status 410 "$(post "$JAR_A" unlock "{\"code\":\"$EXPIRED\"}")" "un code de plus de 30 minutes (ici 2 s) est refusé"
dc down -v --remove-orphans >/dev/null 2>&1

# --- B. Parcours nominal ---------------------------------------------------------------------
echo "== Compose vierge"
dc up -d >/dev/null
wait_for 60 "le wizard affiche son code dans les logs" has_code
CODE=$(code_from_logs)

curl -s "$BASE/" | grep -q "Installation de Shameless" || fail "GET / ne sert pas le wizard"
ok "GET / sert le wizard (aucune variable d'Instance n'a été fournie)"
! api_healthy || fail "l'API répond avant l'installation"
ok "l'API n'est pas démarrée avant l'installation"

expect_status 401 "$(post "$JAR_A" install '{}')" "sans code, l'installation est refusée"
expect_status 401 "$(post "$JAR_A" unlock '{"code":"AAAA-AAAA-AAAA-AAAA"}')" "un mauvais code est refusé"
expect_status 200 "$(post "$JAR_A" unlock "{\"code\":\"$CODE\"}")" "le code affiché dans les logs ouvre une session"
expect_status 401 "$(post "$JAR_B" unlock "{\"code\":\"$CODE\"}")" "le code est à usage unique"

INVALIDE="{\"publicUrl\":\"$BASE\",\"clientId\":\"abc\",\"clientSecret\":\"x\",\"guildId\":\"1\",\"adminIds\":\"\"}"
expect_status 422 "$(post "$JAR_A" install "$INVALIDE")" "une saisie invalide est expliquée et refusée"
grep -q '"clientId"' "$TMP/body" || fail "l'erreur ne nomme pas le champ clientId"
! api_healthy || fail "l'API a démarré malgré l'erreur"

echo "== Installation"
VALIDE="{\"publicUrl\":\"$BASE\",\"clientId\":\"$CLIENT_ID\",\"clientSecret\":\"$CLIENT_SECRET\",\"guildId\":\"$GUILD_ID\",\"adminIds\":\"$ADMIN_ID\"}"
expect_status 202 "$(post "$JAR_A" install "$VALIDE")" "la saisie valide est acceptée"

wait_for 240 "l'API démarre seule et répond Healthy via le proxy" api_healthy

curl -s "$BASE/" | grep -q "<title>Shameless</title>" || fail "GET / ne sert pas le site"
ok "GET / sert le site Shameless"

login_redirects_to_discord() {
  local h; h=$(curl -s -D - -o /dev/null "$BASE/api/auth/discord/login")
  echo "$h" | grep -qi '^location: https://discord.com/api/oauth2/authorize' &&
    echo "$h" | grep -q "client_id=$CLIENT_ID" &&
    echo "$h" | grep -q 'redirect_uri=http%3A%2F%2Flocalhost%3A'"$PORT"'%2Fapi%2Fauth%2Fdiscord%2Fcallback'
}
login_redirects_to_discord || fail "la connexion Discord ne redirige pas vers Discord avec le bon callback"
ok "la connexion Discord est possible : redirection vers Discord avec le callback calculé"

wizard_stopped() { [ "$(dc ps -a --format '{{.State}}' wizard)" = "exited" ]; }
wait_for 60 "le wizard s'arrête après la confirmation" wizard_stopped
[ "$(dc ps -a --format '{{.ExitCode}}' wizard)" = "0" ] || fail "le wizard s'est arrêté en erreur"
[ "$(curl -s -o /dev/null -w '%{http_code}' "$SETUP/state")" != "200" ] || fail "le wizard répond encore"
ok "plus aucun wizard n'écoute"

echo "== Secrets"
PG_PASSWORD=$(dc run --rm --no-deps -T --entrypoint cat api /instance/instance.env 2>/dev/null | sed -n "s/^POSTGRES_PASSWORD='\(.*\)'$/\1/p")
[ -n "$PG_PASSWORD" ] || fail "le mot de passe de base généré est introuvable dans instance.env"
ok "un mot de passe de base a été généré automatiquement"
LOGS=$(dc logs --no-color 2>&1)
for secret in "$CLIENT_SECRET" "$PG_PASSWORD"; do
  if grep -qF -- "$secret" <<<"$LOGS"; then fail "un secret apparaît dans les logs"; fi
done
ok "ni le Client Secret saisi ni le mot de passe généré ne sont dans les logs"
if grep -qF -- "$CODE" <<<"$(curl -s "$BASE/")"; then fail "le code est exposé"; fi

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

echo "Acceptation OK"
