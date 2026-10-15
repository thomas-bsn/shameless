using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Soirees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "soiree_id",
                table: "best_of_series",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "soirees",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    status = table.Column<string>(type: "text", nullable: false),
                    opened_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    closed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_soirees", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_best_of_series_soiree_id",
                table: "best_of_series",
                column: "soiree_id");

            migrationBuilder.CreateIndex(
                name: "ix_soirees_une_seule_ouverte",
                table: "soirees",
                column: "status",
                unique: true,
                filter: "status = 'open'");

            migrationBuilder.AddForeignKey(
                name: "fk_best_of_series_soirees_soiree_id",
                table: "best_of_series",
                column: "soiree_id",
                principalTable: "soirees",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Reconstitue les Soirées des séries existantes : une Soirée fermée par nuit,
            // coupure à 6 h heure de Paris (les dates sont stockées en UTC sans fuseau).
            // La Soirée s'ouvre à sa première série et se ferme à sa dernière série ou game.
            // Une nuit a une seule première série, donc opened_at identifie la Soirée insérée.
            migrationBuilder.Sql("""
                WITH series_nuit AS (
                    SELECT s.id,
                           ((s.played_at AT TIME ZONE 'UTC') AT TIME ZONE 'Europe/Paris' - interval '6 hours')::date AS nuit,
                           s.played_at AS debut,
                           GREATEST(s.played_at, MAX(g.played_at)) AS fin
                    FROM best_of_series s
                    LEFT JOIN custom_games g ON g.series_id = s.id
                    GROUP BY s.id, s.played_at
                ),
                nuits AS (
                    SELECT nuit, MIN(debut) AS opened_at, MAX(fin) AS closed_at
                    FROM series_nuit
                    GROUP BY nuit
                ),
                inserees AS (
                    INSERT INTO soirees (status, opened_at, closed_at)
                    SELECT 'closed', opened_at, closed_at FROM nuits ORDER BY nuit
                    RETURNING id, opened_at
                )
                UPDATE best_of_series s
                SET soiree_id = i.id
                FROM series_nuit sn
                JOIN nuits n ON n.nuit = sn.nuit
                JOIN inserees i ON i.opened_at = n.opened_at
                WHERE s.id = sn.id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_best_of_series_soirees_soiree_id",
                table: "best_of_series");

            migrationBuilder.DropTable(
                name: "soirees");

            migrationBuilder.DropIndex(
                name: "ix_best_of_series_soiree_id",
                table: "best_of_series");

            migrationBuilder.DropColumn(
                name: "soiree_id",
                table: "best_of_series");
        }
    }
}
