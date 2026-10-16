using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RapportsUniquesParIdentifiantRiot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Un renvoi d'un rapport rejeté a pu en garder un doublon : seul le premier reçu reste.
            migrationBuilder.Sql("""
                DELETE FROM rapports_companion r
                USING rapports_companion premier
                WHERE premier.riot_match_id = r.riot_match_id AND premier.id < r.id;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_rapports_companion_riot_match_id",
                table: "rapports_companion",
                column: "riot_match_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_rapports_companion_riot_match_id",
                table: "rapports_companion");
        }
    }
}
