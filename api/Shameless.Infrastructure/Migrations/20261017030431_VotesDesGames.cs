using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VotesDesGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bulletins_de_vote",
                columns: table => new
                {
                    game_id = table.Column<int>(type: "integer", nullable: false),
                    votant_discord_id = table.Column<long>(type: "bigint", nullable: false),
                    mvp_player_id = table.Column<int>(type: "integer", nullable: true),
                    dofd_player_id = table.Column<int>(type: "integer", nullable: true),
                    vote_le = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bulletins_de_vote", x => new { x.game_id, x.votant_discord_id });
                    table.ForeignKey(
                        name: "fk_bulletins_de_vote_custom_games_game_id",
                        column: x => x.game_id,
                        principalTable: "custom_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bulletins_de_vote_players_dofd_player_id",
                        column: x => x.dofd_player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bulletins_de_vote_players_mvp_player_id",
                        column: x => x.mvp_player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "spectateurs_de_game",
                columns: table => new
                {
                    game_id = table.Column<int>(type: "integer", nullable: false),
                    discord_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spectateurs_de_game", x => new { x.game_id, x.discord_id });
                    table.ForeignKey(
                        name: "fk_spectateurs_de_game_custom_games_game_id",
                        column: x => x.game_id,
                        principalTable: "custom_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bulletins_de_vote_dofd_player_id",
                table: "bulletins_de_vote",
                column: "dofd_player_id");

            migrationBuilder.CreateIndex(
                name: "ix_bulletins_de_vote_mvp_player_id",
                table: "bulletins_de_vote",
                column: "mvp_player_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bulletins_de_vote");

            migrationBuilder.DropTable(
                name: "spectateurs_de_game");
        }
    }
}
