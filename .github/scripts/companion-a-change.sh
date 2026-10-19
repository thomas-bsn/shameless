#!/usr/bin/env bash
# Dit « oui » si l'exécutable du Companion doit être republié entre deux commits, « non » sinon.
#
# Un fichier sous companion/ change l'exécutable (code, projets, solution, global.json, profil de
# publication), sauf :
#   companion/spike/   l'outil de capture jetable n'est pas dans l'exécutable ;
#   companion/tests/   les tests ne changent pas l'exécutable ;
#   *.md               la documentation non plus.
#
# Usage : companion-a-change.sh AVANT APRES
set -euo pipefail

avant=${1:?commit de départ}
apres=${2:?commit final}

# La liste est lue en entier avant le test : un « grep -q » dans le tube fermerait l'entrée trop tôt
# (SIGPIPE) et ferait répondre « non » à tort.
pertinents=$(git diff --name-only "$avant" "$apres" | grep -E '^companion/' | grep -Ev '^companion/(spike|tests)/|\.md$' || true)

if [[ -n $pertinents ]]; then
  echo oui
else
  echo non
fi
