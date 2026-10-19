using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChatDeLaDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "drafts_champions_messages",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    draft_champions_id = table.Column<int>(type: "integer", nullable: false),
                    emetteur = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    auteur = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    equipe = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    contenu = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    envoye_le = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_drafts_champions_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_drafts_champions_messages_drafts_champions_draft_champions_",
                        column: x => x.draft_champions_id,
                        principalTable: "drafts_champions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_drafts_champions_messages_draft_champions_id_emetteur_envoy",
                table: "drafts_champions_messages",
                columns: new[] { "draft_champions_id", "emetteur", "envoye_le" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "drafts_champions_messages");
        }
    }
}
