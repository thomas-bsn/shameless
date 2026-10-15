using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "best_of_series",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    played_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    format = table.Column<int>(type: "integer", nullable: false),
                    team_a_wins = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    team_b_wins = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    team_a_reference = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("best_of_series_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    discord_id = table.Column<long>(type: "bigint", nullable: false),
                    discord_username = table.Column<string>(type: "character varying", nullable: true),
                    avatar_url = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pk", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "custom_games",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    played_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    winner = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    riot_match_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    riot_match_data = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    series_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_games", x => x.id);
                    table.ForeignKey(
                        name: "custom_games_series_id_fkey",
                        column: x => x.series_id,
                        principalTable: "best_of_series",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    puuid = table.Column<string>(type: "character varying", nullable: false),
                    prenom = table.Column<string>(type: "text", nullable: false),
                    riot_id = table.Column<string>(type: "text", nullable: false),
                    rank_tier = table.Column<string>(type: "text", nullable: true),
                    rank_division = table.Column<int>(type: "integer", nullable: true),
                    main_role = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    lp = table.Column<int>(type: "integer", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    peak_tier = table.Column<string>(type: "text", nullable: true),
                    peak_division = table.Column<int>(type: "integer", nullable: true),
                    peak_season = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    peak_lp = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    current_cost = table.Column<decimal>(type: "numeric(3,1)", nullable: false, defaultValue: 0m),
                    is_placement = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("players_pkey", x => x.id);
                    table.ForeignKey(
                        name: "players_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
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
                    riot_participant_data = table.Column<string>(type: "jsonb", nullable: true)
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
                name: "ix_custom_games_series_id",
                table: "custom_games",
                column: "series_id");

            migrationBuilder.CreateIndex(
                name: "ix_player_monthly_costs_player_id_month",
                table: "player_monthly_costs",
                columns: new[] { "player_id", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_players_user_id",
                table: "players",
                column: "user_id",
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

            migrationBuilder.DropTable(
                name: "players");

            migrationBuilder.DropTable(
                name: "best_of_series");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
