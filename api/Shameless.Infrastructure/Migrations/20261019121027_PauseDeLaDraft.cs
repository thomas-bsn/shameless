using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PauseDeLaDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "pause_commence_le",
                table: "drafts_champions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pause_par_equipe",
                table: "drafts_champions",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "pause_utilisee_a",
                table: "drafts_champions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "pause_utilisee_b",
                table: "drafts_champions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "reprise_demandee_le",
                table: "drafts_champions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reprise_demandee_par_equipe",
                table: "drafts_champions",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "temps_restant_avant_pause",
                table: "drafts_champions",
                type: "interval",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pause_commence_le",
                table: "drafts_champions");

            migrationBuilder.DropColumn(
                name: "pause_par_equipe",
                table: "drafts_champions");

            migrationBuilder.DropColumn(
                name: "pause_utilisee_a",
                table: "drafts_champions");

            migrationBuilder.DropColumn(
                name: "pause_utilisee_b",
                table: "drafts_champions");

            migrationBuilder.DropColumn(
                name: "reprise_demandee_le",
                table: "drafts_champions");

            migrationBuilder.DropColumn(
                name: "reprise_demandee_par_equipe",
                table: "drafts_champions");

            migrationBuilder.DropColumn(
                name: "temps_restant_avant_pause",
                table: "drafts_champions");
        }
    }
}
