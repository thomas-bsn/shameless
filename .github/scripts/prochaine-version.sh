#!/usr/bin/env bash
# Calcule la prochaine version stable X.Y.Z (sans le « v ») à partir des tags du dépôt courant.
#
# Usage : prochaine-version.sh [TITRE_PR [LABELS_PR]]
#   TITRE_PR   titre de la PR mergée ; « [major] » ou « [minor] » (insensible à la casse) force le niveau.
#   LABELS_PR  labels de la PR, séparés par des retours à la ligne ou des virgules ; « major » / « minor ».
#
# Base : le plus grand tag vX.Y.Z (les préversions vX.Y.Z-rc.N et tout autre tag sont ignorés ;
# sans tag, la base est 0.0.0). Niveau : major > minor > patch (défaut).
# Entrées passées en arguments (jamais interprétées par un shell) : sûr avec des titres arbitraires.
set -euo pipefail

titre=${1-}
labels=${2-}

dernier=$(git tag --list 'v*' | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | sort -V | tail -n 1 || true)
IFS=. read -r major minor patch <<<"${dernier#v}"
major=${major:-0}; minor=${minor:-0}; patch=${patch:-0}

minuscules() { printf '%s' "$1" | tr '[:upper:]' '[:lower:]'; }
titre_min=$(minuscules "$titre")
labels_min=$(minuscules "$labels")

a_label() { printf '%s\n' "$labels_min" | tr ',' '\n' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//' | grep -qx "$1"; }

if [[ $titre_min == *"[major]"* ]] || a_label major; then
  niveau=major
elif [[ $titre_min == *"[minor]"* ]] || a_label minor; then
  niveau=minor
else
  niveau=patch
fi

case $niveau in
  major) major=$((major + 1)); minor=0; patch=0 ;;
  minor) minor=$((minor + 1)); patch=0 ;;
  patch) patch=$((patch + 1)) ;;
esac

echo "${major}.${minor}.${patch}"
