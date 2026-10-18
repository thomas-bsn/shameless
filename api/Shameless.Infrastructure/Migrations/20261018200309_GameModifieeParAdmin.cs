using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GameModifieeParAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Une base qui a servi une ancienne version du projet peut déjà porter cette colonne.
            migrationBuilder.Sql(
                "ALTER TABLE custom_games ADD COLUMN IF NOT EXISTS modifiee_par_admin_le timestamp without time zone NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "modifiee_par_admin_le",
                table: "custom_games");
        }
    }
}
