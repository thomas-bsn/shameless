using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PronosticsDesSpectateurs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pronostics",
                columns: table => new
                {
                    series_id = table.Column<int>(type: "integer", nullable: false),
                    numero_game = table.Column<int>(type: "integer", nullable: false),
                    pronostiqueur_discord_id = table.Column<long>(type: "bigint", nullable: false),
                    pronostiqueur_nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    equipe_gagnante = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    mvp_player_id = table.Column<int>(type: "integer", nullable: true),
                    dofd_player_id = table.Column<int>(type: "integer", nullable: true),
                    pronostique_le = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pronostics", x => new { x.series_id, x.numero_game, x.pronostiqueur_discord_id });
                    table.CheckConstraint("ck_pronostics_equipe_gagnante", "equipe_gagnante IS NULL OR equipe_gagnante IN ('A', 'B')");
                    table.ForeignKey(
                        name: "fk_pronostics_best_of_series_series_id",
                        column: x => x.series_id,
                        principalTable: "best_of_series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pronostics_players_dofd_player_id",
                        column: x => x.dofd_player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pronostics_players_mvp_player_id",
                        column: x => x.mvp_player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pronostics_dofd_player_id",
                table: "pronostics",
                column: "dofd_player_id");

            migrationBuilder.CreateIndex(
                name: "ix_pronostics_mvp_player_id",
                table: "pronostics",
                column: "mvp_player_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pronostics");
        }
    }
}
