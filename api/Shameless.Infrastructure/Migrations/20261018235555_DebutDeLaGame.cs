using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DebutDeLaGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "started_at",
                table: "custom_games",
                type: "timestamp without time zone",
                nullable: true);

            // Les games existantes ont commencé à leur lancement (ADR 0013, avant l'ADR 0017).
            migrationBuilder.Sql("UPDATE custom_games SET started_at = created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "started_at",
                table: "custom_games");
        }
    }
}
