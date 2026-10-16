using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CodesCompanion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "code_companion",
                table: "best_of_series",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Les Séries existantes reçoivent leur code Companion : un secret de 64 caractères tiré de
            // deux UUID aléatoires (244 bits), comme les liens d'accès (CapitainesEtLiensAcces).
            migrationBuilder.Sql(@"
                UPDATE best_of_series
                SET code_companion = replace(gen_random_uuid()::text, '-', '') || replace(gen_random_uuid()::text, '-', '');
                ALTER TABLE best_of_series ALTER COLUMN code_companion DROP DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "ix_best_of_series_code_companion",
                table: "best_of_series",
                column: "code_companion",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_best_of_series_code_companion",
                table: "best_of_series");

            migrationBuilder.DropColumn(
                name: "code_companion",
                table: "best_of_series");
        }
    }
}
