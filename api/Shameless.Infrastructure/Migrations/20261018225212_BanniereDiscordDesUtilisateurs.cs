using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BanniereDiscordDesUtilisateurs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Une base qui a servi une ancienne tentative peut déjà porter ces colonnes : on ignore celles qui existent.
            migrationBuilder.Sql("ALTER TABLE users ADD COLUMN IF NOT EXISTS banner_color character varying(7) NULL;");
            migrationBuilder.Sql("ALTER TABLE users ADD COLUMN IF NOT EXISTS banner_url character varying NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "banner_color",
                table: "users");

            migrationBuilder.DropColumn(
                name: "banner_url",
                table: "users");
        }
    }
}
