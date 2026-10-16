using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RapportsEnRevue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rapports_companion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    game_id = table.Column<int>(type: "integer", nullable: false),
                    riot_match_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    contenu = table.Column<string>(type: "jsonb", nullable: false),
                    statut = table.Column<string>(type: "text", nullable: false),
                    recu_le = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    decide_le = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rapports_companion", x => x.id);
                    table.ForeignKey(
                        name: "fk_rapports_companion_custom_games_game_id",
                        column: x => x.game_id,
                        principalTable: "custom_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rapports_companion_game_id_en_revue",
                table: "rapports_companion",
                column: "game_id",
                unique: true,
                filter: "statut = 'en_revue'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rapports_companion");
        }
    }
}
