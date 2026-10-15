using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IssueEtCompositionDesSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "alternate_sides",
                table: "best_of_series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "fearless",
                table: "best_of_series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "outcome",
                table: "best_of_series",
                type: "text",
                nullable: false,
                defaultValue: "en_cours");

            migrationBuilder.AddColumn<string>(
                name: "winning_team",
                table: "best_of_series",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "series_players",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    series_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    equipe = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_series_players", x => x.id);
                    table.ForeignKey(
                        name: "fk_series_players_best_of_series_series_id",
                        column: x => x.series_id,
                        principalTable: "best_of_series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_series_players_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_series_players_player_id",
                table: "series_players",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_series_players_series_id_player_id",
                table: "series_players",
                columns: new[] { "series_id", "player_id" },
                unique: true);

            // Issue des séries existantes, à partir du score : gagnée quand une équipe a atteint le
            // nombre de victoires du format, nulle pour un BO2 à 1-1. Les autres n'iront jamais au
            // bout (leurs Soirées reconstituées sont fermées) : elles sont interrompues, sans
            // vainqueur, et ne bloquent pas la fermeture automatique.
            // La composition courante de chaque Équipe est celle de la dernière game de la série.
            migrationBuilder.Sql("""
                UPDATE best_of_series
                SET outcome = CASE
                        WHEN team_a_wins >= format / 2 + 1 OR team_b_wins >= format / 2 + 1 THEN 'gagnee'
                        WHEN format = 2 AND team_a_wins = 1 AND team_b_wins = 1 THEN 'nulle'
                        ELSE 'interrompue'
                    END,
                    winning_team = CASE
                        WHEN team_a_wins >= format / 2 + 1 THEN 'A'
                        WHEN team_b_wins >= format / 2 + 1 THEN 'B'
                    END;

                INSERT INTO series_players (series_id, player_id, equipe)
                SELECT DISTINCT ON (g.series_id, cp.player_id)
                       g.series_id, cp.player_id, CASE WHEN cp.team = g.team_a_side THEN 'A' ELSE 'B' END
                FROM (
                    SELECT DISTINCT ON (series_id) id, series_id, team_a_side
                    FROM custom_games
                    WHERE series_id IS NOT NULL
                    ORDER BY series_id, id DESC
                ) g
                JOIN custom_game_participants cp ON cp.custom_game_id = g.id
                ORDER BY g.series_id, cp.player_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "series_players");

            migrationBuilder.DropColumn(
                name: "alternate_sides",
                table: "best_of_series");

            migrationBuilder.DropColumn(
                name: "fearless",
                table: "best_of_series");

            migrationBuilder.DropColumn(
                name: "outcome",
                table: "best_of_series");

            migrationBuilder.DropColumn(
                name: "winning_team",
                table: "best_of_series");
        }
    }
}
