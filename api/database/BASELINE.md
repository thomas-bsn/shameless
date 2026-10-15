# Migration Baseline

La migration `20261015183048_Baseline` remplace les 6 migrations legacy. Elle décrit le schéma réel de la base en production, capturé dans [`legacy-schema.sql`](legacy-schema.sql) (dump `pg_dump --schema-only`, sans données).

C'est le modèle EF qui a été ajusté pour coller à ce schéma : noms de clés primaires et étrangères legacy, colonnes `serial` pour `players`, `users` et `best_of_series`, colonnes `identity` pour les autres tables, valeurs par défaut.

## Vérification

Comparaison faite à la main le 2026-10-15 : Baseline appliquée sur un Postgres 16 vide, puis `pg_dump --schema-only` comparé à `legacy-schema.sql`.

## Écarts restants

| Écart | Base réelle | Baseline | Pourquoi on garde la Baseline |
|---|---|---|---|
| `best_of_series.team_a_wins` / `team_b_wins` | nullable, défaut 0 | `NOT NULL`, défaut 0 | Le code les incrémente comme des entiers ; aucune valeur nulle dans les données réelles. |
| `players.created_at` | nullable, défaut `now()` | `NOT NULL`, défaut `now()` | Toujours rempli par le code ; aucune valeur nulle dans les données réelles. |
| `ix_players_user_id` | absent | index unique sur `players.user_id` | EF l'impose pour la relation 1-1 utilisateur ↔ joueur ; aucun doublon dans les données réelles. |
| Ordre des colonnes | `custom_games`, `players`, `users` | ordre différent | Sans effet fonctionnel. |

## Procédure sur la base migrée

À exécuter une seule fois sur la copie de la base legacy, avant le premier démarrage de Shameless. Ne jamais l'exécuter sur l'instance legacy en service.

1. Aligner le schéma sur la Baseline (échoue proprement si une donnée viole une contrainte) :

   ```sql
   BEGIN;
   UPDATE best_of_series SET team_a_wins = 0 WHERE team_a_wins IS NULL;
   UPDATE best_of_series SET team_b_wins = 0 WHERE team_b_wins IS NULL;
   ALTER TABLE best_of_series ALTER COLUMN team_a_wins SET NOT NULL;
   ALTER TABLE best_of_series ALTER COLUMN team_b_wins SET NOT NULL;
   UPDATE players SET created_at = now() WHERE created_at IS NULL;
   ALTER TABLE players ALTER COLUMN created_at SET NOT NULL;
   CREATE UNIQUE INDEX ix_players_user_id ON players (user_id);
   COMMIT;
   ```

2. Remplacer l'historique des migrations legacy (7 lignes, dont un `AddBestOfSeries` en double) par la Baseline, marquée comme appliquée :

   ```sql
   BEGIN;
   DELETE FROM "__EFMigrationsHistory";
   INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
   VALUES ('20261015183048_Baseline', '10.0.13');
   COMMIT;
   ```

3. Lancer le bundle de migration (service `migrate` du compose) : il ne doit rien appliquer.
