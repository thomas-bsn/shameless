using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Clips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clips",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    discord_message_id = table.Column<long>(type: "bigint", nullable: false),
                    discord_attachment_id = table.Column<long>(type: "bigint", nullable: false),
                    auteur_discord_id = table.Column<long>(type: "bigint", nullable: false),
                    auteur_nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    publie_le = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    recu_le = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    nom_fichier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    taille = table.Column<long>(type: "bigint", nullable: false),
                    fichier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    game_id = table.Column<int>(type: "integer", nullable: true),
                    rattachement = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clips", x => x.id);
                    table.CheckConstraint("ck_clips_rattachement", "rattachement IN ('automatique', 'manuel')");
                    table.ForeignKey(
                        name: "fk_clips_custom_games_game_id",
                        column: x => x.game_id,
                        principalTable: "custom_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clips_discord_attachment_id",
                table: "clips",
                column: "discord_attachment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clips_game_id",
                table: "clips",
                column: "game_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clips");
        }
    }
}
