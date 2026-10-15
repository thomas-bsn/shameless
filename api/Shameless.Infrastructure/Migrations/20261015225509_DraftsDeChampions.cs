using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DraftsDeChampions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "drafts_champions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    game_id = table.Column<int>(type: "integer", nullable: false),
                    tour_commence_le = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_drafts_champions", x => x.id);
                    table.ForeignKey(
                        name: "fk_drafts_champions_custom_games_game_id",
                        column: x => x.game_id,
                        principalTable: "custom_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "drafts_champions_actions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    draft_champions_id = table.Column<int>(type: "integer", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    champion_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_drafts_champions_actions", x => x.id);
                    table.ForeignKey(
                        name: "fk_drafts_champions_actions_drafts_champions_draft_champions_id",
                        column: x => x.draft_champions_id,
                        principalTable: "drafts_champions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_drafts_champions_game_id",
                table: "drafts_champions",
                column: "game_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_drafts_champions_actions_draft_champions_id_champion_id",
                table: "drafts_champions_actions",
                columns: new[] { "draft_champions_id", "champion_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_drafts_champions_actions_draft_champions_id_numero",
                table: "drafts_champions_actions",
                columns: new[] { "draft_champions_id", "numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "drafts_champions_actions");

            migrationBuilder.DropTable(
                name: "drafts_champions");
        }
    }
}
