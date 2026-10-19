using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SupprimerLiensCapitaineEnvoyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "liens_capitaine_envoyes",
                table: "best_of_series");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "liens_capitaine_envoyes",
                table: "best_of_series",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
