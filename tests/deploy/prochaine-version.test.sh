#!/usr/bin/env bash
# Teste .github/scripts/prochaine-version.sh dans des dépôts git temporaires (tags simulés).
# Usage : tests/deploy/prochaine-version.test.sh
set -u
racine=$(cd "$(dirname "$0")/../.." && pwd)
script=$racine/.github/scripts/prochaine-version.sh
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
echec=0

# attendre "libellé" "version attendue" "titre" "labels" tag...
attendre() {
  local libelle=$1 attendu=$2 titre=$3 labels=$4; shift 4
  local depot
  depot=$(mktemp -d "$tmp/depot.XXXXXX")
  (
    cd "$depot" || exit 1
    git init -q .
    git -c user.name=t -c user.email=t@t commit -q --allow-empty -m init
    for t in "$@"; do git tag "$t"; done
  )
  local obtenu
  obtenu=$(cd "$depot" && "$script" "$titre" "$labels" 2>&1)
  if [[ $obtenu == "$attendu" ]]; then echo "ok   $libelle ($obtenu)"; else echo "FAIL $libelle : attendu $attendu, obtenu $obtenu"; echec=1; fi
}

attendre "patch par défaut"                     1.2.4  "Corrige un bug"  ""            v1.2.3
attendre "sans argument"                        1.2.4  ""                ""            v1.2.3
attendre "label minor"                          1.3.0  "Ajoute un truc"  "minor"       v1.2.3
attendre "[minor] dans le titre"                1.3.0  "[minor] Truc"    ""            v1.2.3
attendre "[MINOR] insensible à la casse"        1.3.0  "[MINOR] Truc"    ""            v1.2.3
attendre "label major"                          2.0.0  "Refonte"         "major"       v1.2.3
attendre "[major] dans le titre"                2.0.0  "Refonte [major]" ""            v1.2.3
attendre "major l'emporte sur minor"            2.0.0  "[minor] x"       "major"       v1.2.3
attendre "plusieurs labels, dont minor"         1.3.0  "x"               $'bug\nminor' v1.2.3
attendre "labels séparés par des virgules"      1.3.0  "x"               "bug, Minor"  v1.2.3
attendre "label qui contient minor sans l'être" 1.2.4  "x"               "minority"    v1.2.3
attendre "mot minor sans crochets"              1.2.4  "Fix minor glitch" ""           v1.2.3
attendre "absence de tag"                       0.0.1  "x"               ""
attendre "absence de tag, minor"                0.1.0  "x"               "minor"
attendre "absence de tag, major"                1.0.0  "x"               "major"
attendre "préversion ignorée"                   1.2.4  "x"               ""            v1.2.3 v1.3.0-rc.1
attendre "seules des préversions"               0.0.1  "x"               ""            v1.0.0-rc.1
attendre "tri numérique (1.10 > 1.9)"           1.10.1 "x"               ""            v1.9.5 v1.10.0 v1.2.0
attendre "tags non semver ignorés"              1.2.4  "x"               ""            v1.2.3 vnext v2 v1.2.3.4 latest

exit $echec
