using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LiensEnvoyesAuBot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_evenements_bot_type_series_id",
                table: "evenements_bot");

            migrationBuilder.AddColumn<string>(
                name: "equipe",
                table: "evenements_bot",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_evenements_bot_type_series_id",
                table: "evenements_bot",
                columns: new[] { "type", "series_id" },
                unique: true,
                filter: "series_id IS NOT NULL AND equipe IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_evenements_bot_type_series_id_equipe",
                table: "evenements_bot",
                columns: new[] { "type", "series_id", "equipe" },
                unique: true,
                filter: "series_id IS NOT NULL AND equipe IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_evenements_bot_type_series_id",
                table: "evenements_bot");

            migrationBuilder.DropIndex(
                name: "ix_evenements_bot_type_series_id_equipe",
                table: "evenements_bot");

            migrationBuilder.DropColumn(
                name: "equipe",
                table: "evenements_bot");

            migrationBuilder.CreateIndex(
                name: "ix_evenements_bot_type_series_id",
                table: "evenements_bot",
                columns: new[] { "type", "series_id" },
                unique: true,
                filter: "series_id IS NOT NULL");
        }
    }
}
