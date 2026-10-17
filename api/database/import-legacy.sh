#!/bin/sh
# Remplace la base d'une Instance Shameless toute neuve par les données d'un dump complet de
# l'ancien site (lol_customs). À lancer dans le conteneur postgres :
#   sh import-legacy.sh /tmp/dump.sql
# Procédure de BASELINE.md : le dump est chargé tel quel, aligné sur la Baseline, son historique de
# migrations est remplacé par la Baseline ; il reste à relancer le service migrate, qui rejoue les
# migrations suivantes et convertit les données (équipes, issues des séries, codes Companion...).
# Refuse de s'exécuter si l'Instance contient déjà des données (sauf FORCE=1, qui les efface).
set -eu

DUMP=${1:?usage : sh import-legacy.sh /chemin/vers/dump.sql}
BASE=${POSTGRES_DB:-shameless}
UTILISATEUR=${POSTGRES_USER:-shameless}

sql() { psql -v ON_ERROR_STOP=1 -q -U "$UTILISATEUR" -d "$BASE" "$@"; }

[ -r "$DUMP" ] || { echo "Dump illisible : $DUMP" >&2; exit 1; }

if [ "${FORCE:-0}" != 1 ]; then
  for t in players custom_games best_of_series; do
    n=$(sql -Atc "SELECT count(*) FROM $t" 2>/dev/null || echo 0)
    [ "$n" = 0 ] || { echo "La table $t contient $n ligne(s) : import annulé, rien n'a été modifié. FORCE=1 pour tout effacer." >&2; exit 1; }
  done
fi

echo "Chargement du dump (le schéma actuel de la base est remplacé)..."
sql -c "DROP SCHEMA public CASCADE" -c "CREATE SCHEMA public"
sql -f "$DUMP" >/dev/null

echo "Alignement sur la Baseline..."
sql <<'SQL'
BEGIN;
UPDATE best_of_series SET team_a_wins = 0 WHERE team_a_wins IS NULL;
UPDATE best_of_series SET team_b_wins = 0 WHERE team_b_wins IS NULL;
ALTER TABLE best_of_series ALTER COLUMN team_a_wins SET NOT NULL;
ALTER TABLE best_of_series ALTER COLUMN team_b_wins SET NOT NULL;
UPDATE players SET created_at = now() WHERE created_at IS NULL;
ALTER TABLE players ALTER COLUMN created_at SET NOT NULL;
CREATE UNIQUE INDEX ix_players_user_id ON players (user_id);
DELETE FROM "__EFMigrationsHistory";
INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261015183048_Baseline', '10.0.13');
COMMIT;
SQL

for t in users players best_of_series custom_games custom_game_participants player_monthly_costs; do
  echo "$t : $(sql -Atc "SELECT count(*) FROM $t") ligne(s)"
done
echo "Dump chargé. Relancez maintenant le service migrate pour appliquer les migrations suivantes."
