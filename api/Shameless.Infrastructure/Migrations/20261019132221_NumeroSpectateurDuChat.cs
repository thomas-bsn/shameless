using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NumeroSpectateurDuChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "numero_spectateur",
                table: "drafts_champions_messages",
                type: "integer",
                nullable: true);

            // Chats déjà ouverts : le premier message de chaque spectateur sans connexion reçoit son numéro.
            migrationBuilder.Sql(@"
                UPDATE drafts_champions_messages m SET numero_spectateur = n.numero
                FROM (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY draft_champions_id ORDER BY id) AS numero
                    FROM drafts_champions_messages x
                    WHERE emetteur LIKE 'anonyme:%'
                      AND id = (SELECT MIN(id) FROM drafts_champions_messages y
                                WHERE y.draft_champions_id = x.draft_champions_id AND y.emetteur = x.emetteur)
                ) n WHERE m.id = n.id;");

            migrationBuilder.CreateIndex(
                name: "ix_drafts_champions_messages_draft_champions_id_emetteur",
                table: "drafts_champions_messages",
                columns: new[] { "draft_champions_id", "emetteur" },
                unique: true,
                filter: "numero_spectateur IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_drafts_champions_messages_draft_champions_id_numero_spectat",
                table: "drafts_champions_messages",
                columns: new[] { "draft_champions_id", "numero_spectateur" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_drafts_champions_messages_draft_champions_id_emetteur",
                table: "drafts_champions_messages");

            migrationBuilder.DropIndex(
                name: "ix_drafts_champions_messages_draft_champions_id_numero_spectat",
                table: "drafts_champions_messages");

            migrationBuilder.DropColumn(
                name: "numero_spectateur",
                table: "drafts_champions_messages");
        }
    }
}
