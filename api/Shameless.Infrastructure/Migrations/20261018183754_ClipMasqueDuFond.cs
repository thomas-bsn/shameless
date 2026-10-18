using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClipMasqueDuFond : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Une base qui a servi une ancienne version de la refonte du front porte déjà cette colonne.
            migrationBuilder.Sql(
                "ALTER TABLE clips ADD COLUMN IF NOT EXISTS masque_du_fond boolean NOT NULL DEFAULT FALSE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "masque_du_fond",
                table: "clips");
        }
    }
}
