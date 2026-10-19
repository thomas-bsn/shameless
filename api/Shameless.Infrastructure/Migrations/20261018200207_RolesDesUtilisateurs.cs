using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RolesDesUtilisateurs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Une base qui a servi une ancienne tentative peut déjà porter ces colonnes : on ignore celles qui existent.
            migrationBuilder.Sql("ALTER TABLE users ADD COLUMN IF NOT EXISTS promoted_at timestamp without time zone NULL;");
            migrationBuilder.Sql("ALTER TABLE users ADD COLUMN IF NOT EXISTS promoted_by character varying NULL;");
            migrationBuilder.Sql("ALTER TABLE users ADD COLUMN IF NOT EXISTS role character varying(30) NOT NULL DEFAULT 'Utilisateur';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE users DROP COLUMN IF EXISTS promoted_at;");
            migrationBuilder.Sql("ALTER TABLE users DROP COLUMN IF EXISTS promoted_by;");
            migrationBuilder.Sql("ALTER TABLE users DROP COLUMN IF EXISTS role;");
        }
    }
}
