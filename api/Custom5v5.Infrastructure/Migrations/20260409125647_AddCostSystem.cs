using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Custom5v5.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCostSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "current_cost",
                table: "players",
                type: "numeric(3,1)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "is_placement",
                table: "players",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "custom_games",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    played_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    winner = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    riot_match_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    riot_match_data = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_games", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "player_monthly_costs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<DateTime>(type: "date", nullable: false),
                    final_cost = table.Column<decimal>(type: "numeric(3,1)", nullable: false),
                    base_mmr = table.Column<int>(type: "integer", nullable: false),
                    winrate_adjustment = table.Column<int>(type: "integer", nullable: false),
                    games_played = table.Column<int>(type: "integer", nullable: false),
                    wins = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_monthly_costs", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_monthly_costs_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "custom_game_participants",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    custom_game_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    team = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    riot_participant_data = table.Column<string>(type: "jsonb", nullable: true),
                    won = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_game_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_custom_game_participants_custom_games_custom_game_id",
                        column: x => x.custom_game_id,
                        principalTable: "custom_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_custom_game_participants_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_custom_game_participants_custom_game_id_player_id",
                table: "custom_game_participants",
                columns: new[] { "custom_game_id", "player_id" });

            migrationBuilder.CreateIndex(
                name: "ix_custom_game_participants_player_id",
                table: "custom_game_participants",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_games_played_at",
                table: "custom_games",
                column: "played_at");

            migrationBuilder.CreateIndex(
                name: "ix_player_monthly_costs_player_id_month",
                table: "player_monthly_costs",
                columns: new[] { "player_id", "month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custom_game_participants");

            migrationBuilder.DropTable(
                name: "player_monthly_costs");

            migrationBuilder.DropTable(
                name: "custom_games");

            migrationBuilder.DropColumn(
                name: "current_cost",
                table: "players");

            migrationBuilder.DropColumn(
                name: "is_placement",
                table: "players");
        }
    }
}
