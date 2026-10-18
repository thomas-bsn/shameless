using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PresenceDesCapitaines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "rejoint_le",
                table: "liens_acces",
                type: "timestamp without time zone",
                nullable: true);

            // Les séries en cours au déploiement comptent comme déjà rejointes : aucune draft n'est bloquée.
            migrationBuilder.Sql("""
                UPDATE liens_acces l
                SET rejoint_le = now() AT TIME ZONE 'utc'
                FROM best_of_series s
                WHERE l.series_id = s.id AND l.portee = 'capitaine' AND s.outcome = 'en_cours'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "rejoint_le",
                table: "liens_acces");
        }
    }
}
