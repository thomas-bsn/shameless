using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PretDesCapitaines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "pret_le",
                table: "liens_acces",
                type: "timestamp without time zone",
                nullable: true);

            // Une Série dont la game est déjà lancée au déploiement : ses Capitaines comptent comme prêts, pour
            // que l'état affiché soit cohérent avec la draft en cours. Les autres séries en cours attendent
            // leurs « prêt », sans rien de bloqué : les Capitaines ont déjà rejoint (PresenceDesCapitaines).
            migrationBuilder.Sql(
                """
                UPDATE liens_acces
                SET pret_le = COALESCE(rejoint_le, now() AT TIME ZONE 'utc')
                WHERE portee = 'capitaine'
                  AND EXISTS (SELECT 1 FROM custom_games g WHERE g.series_id = liens_acces.series_id AND g.status = 'pending')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pret_le",
                table: "liens_acces");
        }
    }
}
