#!/usr/bin/env bash
# Vérifie (statiquement, sans rien publier) la publication des images et le Compose de déploiement.
# Usage : tests/deploy/verifier-publication.sh   (depuis n'importe où)
set -u
cd "$(dirname "$0")/../.."
echec=0
ok()  { echo "ok   $1"; }
ko()  { echo "FAIL $1"; echec=1; }
verifier() { local msg=$1; shift; if "$@" >/dev/null 2>&1; then ok "$msg"; else ko "$msg"; fi; }

WF=.github/workflows/release.yml
verifier "le workflow de publication existe" test -f "$WF"
verifier "il se déclenche sur les tags vX.Y.Z" grep -Eq "^\s*-\s*['\"]?v\*\.\*\.\*['\"]?" "$WF"
verifier "il ne se déclenche pas sur une branche" bash -c "! grep -Eq '^\s*branches:' $WF"
verifier "il publie sur ghcr.io avec GITHUB_TOKEN" bash -c "grep -q 'ghcr.io' $WF && grep -q 'secrets.GITHUB_TOKEN' $WF && grep -q 'packages: write' $WF"
for img in api migrate proxy wizard; do
  verifier "la matrice contient l'image $img" grep -Eq "name:\s*$img\s*$" "$WF"
done
verifier "le tag latest est posé (versions stables)" grep -q 'latest' "$WF"
verifier "les tags de version sont reproductibles (semver)" grep -q '{{version}}' "$WF"

# Compose de déploiement : uniquement des images, aucun build.
verifier "compose.yaml ne contient aucun build:" bash -c "! grep -Eq '^\s*(build|additional_contexts):' compose.yaml"
verifier "compose.yaml tire les images depuis ghcr.io par défaut" grep -q 'SHAMELESS_IMAGE_PREFIX:-ghcr.io/thomas-bsn/shameless' compose.yaml
verifier "compose.override.yaml porte les build: de dev" bash -c "grep -c 'build:' compose.override.yaml | grep -q 4"
verifier "compose.yaml n'a ni ports: ni user: (refusés par Forgeyard ; voir compose.ports.yaml)" bash -c "! grep -Eq '^\s+(ports|user):' compose.yaml"

# Pas de secret dans les images.
verifier "api/.dockerignore exclut .env" grep -Eq '^\*\*/\.env|^\.env' api/.dockerignore
verifier "web/.dockerignore exclut .env" grep -Eq '^\*\*/\.env|^\.env' web/.dockerignore
verifier "wizard/.dockerignore exclut .env" grep -Eq '^\*\*/\.env|^\.env' wizard/.dockerignore
verifier "les Dockerfile n'embarquent aucun secret" bash -c "! grep -Ein 'password|secret|api_?key' api/Dockerfile deploy/caddy/Dockerfile wizard/Dockerfile"

if command -v docker >/dev/null && docker compose version >/dev/null 2>&1; then
  # Aucune variable d'Instance ne doit être requise : le wizard la collecte.
  unset SHAMELESS_IMAGE_PREFIX SHAMELESS_VERSION
  verifier "compose de déploiement valide (docker compose config)" docker compose -f compose.yaml config
  verifier "compose de dev valide (avec override)" docker compose config
  verifier "le déploiement n'a aucun build (config résolue)" bash -c "! docker compose -f compose.yaml config | grep -q '^\s*build:'"
  verifier "le dev construit les 4 images" bash -c "docker compose config | grep -c '^\s*build:' | grep -q 4"
  verifier "le déploiement cible ghcr.io/thomas-bsn/shameless-api:latest" bash -c "docker compose -f compose.yaml config | grep -q 'image: ghcr.io/thomas-bsn/shameless-api:latest'"
else
  echo "skip docker indisponible : validation du compose ignorée"
fi
exit $echec
