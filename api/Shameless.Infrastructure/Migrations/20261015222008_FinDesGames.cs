using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinDesGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "custom_games",
                type: "timestamp without time zone",
                nullable: true);

            // L'heure de saisie des résultats passés est inconnue : on retient le lancement de la game.
            migrationBuilder.Sql("UPDATE custom_games SET completed_at = played_at WHERE status = 'completed';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "custom_games");
        }
    }
}
