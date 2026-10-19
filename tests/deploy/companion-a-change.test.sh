#!/usr/bin/env bash
# Teste .github/scripts/companion-a-change.sh dans des dépôts git temporaires.
# Usage : tests/deploy/companion-a-change.test.sh
set -u
racine=$(cd "$(dirname "$0")/../.." && pwd)
script=$racine/.github/scripts/companion-a-change.sh
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
echec=0

# attendre "libellé" "oui|non" fichier... : un premier commit contient tous les fichiers, le second
# les modifie ; le script compare les deux.
attendre() {
  local libelle=$1 attendu=$2; shift 2
  local depot f
  depot=$(mktemp -d "$tmp/depot.XXXXXX")
  (
    cd "$depot" || exit 1
    git init -q .
    for f in "$@"; do mkdir -p "$(dirname "$f")"; echo avant > "$f"; done
    git add -A
    git -c user.name=t -c user.email=t@t commit -q -m avant
    for f in "$@"; do echo apres > "$f"; done
    git add -A
    git -c user.name=t -c user.email=t@t commit -q -m apres
  )
  local obtenu
  obtenu=$(cd "$depot" && "$script" HEAD~1 HEAD 2>&1)
  if [[ $obtenu == "$attendu" ]]; then echo "ok   $libelle ($obtenu)"; else echo "FAIL $libelle : attendu $attendu, obtenu $obtenu"; echec=1; fi
}

attendre "le code de l'app change"  oui  companion/src/Shameless.Companion.Core/Connexion.cs
attendre "le SDK change"            oui  companion/global.json
attendre "la solution change"       oui  companion/Shameless.Companion.slnx
attendre "le profil de publication" oui  companion/src/Shameless.Companion.App/Properties/PublishProfiles/Windows.pubxml
attendre "gros diff, code en tête"  oui  companion/src/Shameless.Companion.Core/Connexion.cs $(for i in $(seq 1 3000); do echo "zz/un-dossier-au-nom-assez-long/fichier-$i.txt"; done)
attendre "le README racine change"  non  README.md
attendre "l'API change"             non  api/Shameless.Api/Program.cs
attendre "le site change"           non  web/src/main.tsx
attendre "le CI change"             non  .github/workflows/ci.yml
attendre "seul le spike change"     non  companion/spike/Spike/Program.cs
attendre "seuls les tests changent" non  companion/tests/Shameless.Companion.Core.Tests/ConnexionTests.cs
attendre "la doc du companion"      non  companion/spike/README.md companion/README.md
attendre "spike et code ensemble"   oui  companion/spike/Spike/Program.cs companion/src/Shameless.Companion.App/App.axaml
attendre "doc et code ensemble"     oui  companion/README.md companion/src/Shameless.Companion.Core/Connexion.cs
attendre "autre dossier et companion" oui api/Shameless.Api/Program.cs companion/src/Shameless.Companion.Core/Connexion.cs

exit $echec
