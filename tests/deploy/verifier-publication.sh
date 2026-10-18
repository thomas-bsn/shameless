#!/usr/bin/env bash
# Vérifie (statiquement, sans rien publier) la publication des images et le Compose de déploiement.
# Usage : tests/deploy/verifier-publication.sh   (depuis n'importe où)
set -u
cd "$(dirname "$0")/../.."
echec=0
ok()  { echo "ok   $1"; }
ko()  { echo "FAIL $1"; echec=1; }
verifier() { local msg=$1; shift; if "$@" >/dev/null 2>&1; then ok "$msg"; else ko "$msg"; fi; }

WF=.github/workflows/publier.yml
verifier "le workflow de publication existe (seul, avec ci.yml)" bash -c "test -f $WF && test \$(ls .github/workflows | wc -l) -eq 2"
verifier "il se déclenche après le workflow CI" bash -c "grep -q 'workflow_run:' $WF && grep -q 'workflows: \[CI\]' $WF"
verifier "il a un déclencheur manuel avec une version" bash -c "grep -q 'workflow_dispatch:' $WF && grep -Eq '^\s+version:' $WF"
verifier "il ne se déclenche ni sur un tag ni sur une branche" bash -c "! grep -Eq '^ {0,4}(tags|branches|push):' $WF"
verifier "il n'appelle pas de workflow réutilisable" bash -c "! grep -Eq '^\s*(uses: ./|workflow_call:)' $WF"
verifier "il exige une CI verte" grep -q "conclusion == 'success'" "$WF"
verifier "il se limite aux push sur main" bash -c "grep -q \"event == 'push'\" $WF && grep -q \"head_branch == 'main'\" $WF"
verifier "il construit le commit testé (head_sha)" grep -q 'workflow_run.head_sha' "$WF"
verifier "il sérialise les publications sans annuler" bash -c "grep -q 'cancel-in-progress: false' $WF"
verifier "contents: write seulement dans le job qui pose le tag" bash -c "[ \$(grep -c 'contents: write' $WF) -eq 1 ]"
verifier "il publie sur ghcr.io avec GITHUB_TOKEN et packages: write" bash -c "grep -q 'ghcr.io' $WF && grep -q 'secrets.GITHUB_TOKEN' $WF && grep -q 'packages: write' $WF"
for img in api migrate proxy wizard; do
  verifier "la matrice contient l'image $img" grep -Eq "name:\s*$img\s*$" "$WF"
done
verifier "le tag latest est posé (versions stables)" grep -q 'value=latest' "$WF"
verifier "les tags de version sont reproductibles" grep -q 'steps.versions.outputs.version' "$WF"
verifier "latest et X.Y ne sont posés que pour les versions stables" bash -c "grep -c 'enable=\${{ steps.versions.outputs.stable }}' $WF | grep -q 2"

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
