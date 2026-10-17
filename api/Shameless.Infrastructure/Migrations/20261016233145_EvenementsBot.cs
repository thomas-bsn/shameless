using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EvenementsBot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evenements_bot",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "text", nullable: false),
                    series_id = table.Column<int>(type: "integer", nullable: true),
                    game_id = table.Column<int>(type: "integer", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    statut = table.Column<string>(type: "text", nullable: false),
                    tentatives = table.Column<int>(type: "integer", nullable: false),
                    prochaine_tentative = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    livre_le = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    derniere_erreur = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evenements_bot", x => x.id);
                    table.CheckConstraint("ck_evenements_bot_sujet", "(series_id IS NULL) <> (game_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_evenements_bot_best_of_series_series_id",
                        column: x => x.series_id,
                        principalTable: "best_of_series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_evenements_bot_custom_games_game_id",
                        column: x => x.game_id,
                        principalTable: "custom_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evenements_bot_game_id",
                table: "evenements_bot",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_evenements_bot_series_id",
                table: "evenements_bot",
                column: "series_id");

            migrationBuilder.CreateIndex(
                name: "ix_evenements_bot_statut_prochaine_tentative",
                table: "evenements_bot",
                columns: new[] { "statut", "prochaine_tentative" });

            migrationBuilder.CreateIndex(
                name: "ix_evenements_bot_type_game_id",
                table: "evenements_bot",
                columns: new[] { "type", "game_id" },
                unique: true,
                filter: "game_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_evenements_bot_type_series_id",
                table: "evenements_bot",
                columns: new[] { "type", "series_id" },
                unique: true,
                filter: "series_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evenements_bot");
        }
    }
}
