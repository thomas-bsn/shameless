using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <summary>
    /// Les Équipes A et B deviennent explicites : chaque game enregistre le Side de l'Équipe A
    /// et le vainqueur devient une Équipe. Les vainqueurs existants (en Side) sont convertis à
    /// partir de la référence de RiotIds de l'Équipe A, puis cette référence disparaît.
    /// </summary>
    public partial class EquipesExplicites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "team_a_side",
                table: "custom_games",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "winning_team",
                table: "custom_games",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            // L'Équipe A d'une série : les joueurs de la référence de RiotIds qui ont joué la série.
            // Si la référence est absente ou ne retrouve personne (RiotIds changés), on prend le côté
            // bleu de la première game, qui a servi à écrire la référence.
            // Le Side de l'Équipe A dans une game est celui où l'on retrouve le plus de ses joueurs.
            migrationBuilder.Sql("""
                WITH series_players AS (
                    SELECT DISTINCT g.series_id, cp.player_id
                    FROM custom_games g
                    JOIN custom_game_participants cp ON cp.custom_game_id = g.id
                    WHERE g.series_id IS NOT NULL
                ),
                reference_players AS (
                    SELECT sp.series_id, sp.player_id
                    FROM best_of_series s
                    CROSS JOIN LATERAL json_array_elements_text(
                        CASE WHEN ltrim(s.team_a_reference) LIKE '[%' THEN s.team_a_reference::json ELSE '[]'::json END
                    ) AS ref(riot_id)
                    JOIN players p ON p.riot_id = ref.riot_id
                    JOIN series_players sp ON sp.series_id = s.id AND sp.player_id = p.id
                ),
                first_games AS (
                    SELECT DISTINCT ON (series_id) series_id, id
                    FROM custom_games
                    WHERE series_id IS NOT NULL
                    ORDER BY series_id, id
                ),
                team_a AS (
                    SELECT series_id, player_id FROM reference_players
                    UNION
                    SELECT fg.series_id, cp.player_id
                    FROM first_games fg
                    JOIN custom_game_participants cp ON cp.custom_game_id = fg.id
                    WHERE cp.team = 'blue'
                      AND NOT EXISTS (SELECT 1 FROM reference_players r WHERE r.series_id = fg.series_id)
                ),
                sides AS (
                    SELECT g.id,
                           CASE WHEN count(ta.player_id) FILTER (WHERE cp.team = 'red')
                                   > count(ta.player_id) FILTER (WHERE cp.team = 'blue')
                                THEN 'red' ELSE 'blue' END AS team_a_side
                    FROM custom_games g
                    JOIN custom_game_participants cp ON cp.custom_game_id = g.id
                    LEFT JOIN team_a ta ON ta.series_id = g.series_id AND ta.player_id = cp.player_id
                    GROUP BY g.id
                )
                UPDATE custom_games g
                SET team_a_side = sides.team_a_side
                FROM sides
                WHERE sides.id = g.id;

                UPDATE custom_games SET team_a_side = 'blue' WHERE team_a_side IS NULL;

                UPDATE custom_games
                SET winning_team = CASE WHEN winner = team_a_side THEN 'A' ELSE 'B' END
                WHERE winner IN ('blue', 'red');

                UPDATE best_of_series s
                SET team_a_wins = (SELECT count(*) FROM custom_games g WHERE g.series_id = s.id AND g.winning_team = 'A'),
                    team_b_wins = (SELECT count(*) FROM custom_games g WHERE g.series_id = s.id AND g.winning_team = 'B');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "team_a_side",
                table: "custom_games",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "winner",
                table: "custom_games");

            migrationBuilder.DropColumn(
                name: "team_a_reference",
                table: "best_of_series");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "team_a_reference",
                table: "best_of_series",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "winner",
                table: "custom_games",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE custom_games
                SET winner = CASE winning_team
                    WHEN 'A' THEN team_a_side
                    WHEN 'B' THEN CASE team_a_side WHEN 'blue' THEN 'red' ELSE 'blue' END
                    ELSE '' END;

                UPDATE best_of_series s
                SET team_a_reference = (
                    SELECT json_agg(p.riot_id ORDER BY p.id)::text
                    FROM custom_game_participants cp
                    JOIN custom_games g ON g.id = cp.custom_game_id
                    JOIN players p ON p.id = cp.player_id
                    WHERE g.id = (SELECT min(id) FROM custom_games WHERE series_id = s.id)
                      AND cp.team = g.team_a_side);
                """);

            migrationBuilder.AlterColumn<string>(
                name: "winner",
                table: "custom_games",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "winning_team",
                table: "custom_games");

            migrationBuilder.DropColumn(
                name: "team_a_side",
                table: "custom_games");
        }
    }
}
