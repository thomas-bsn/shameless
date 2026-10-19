using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResetDeLaDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "reset_demande_le",
                table: "drafts_champions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reset_demande_par_equipe",
                table: "drafts_champions",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reset_demande_le",
                table: "drafts_champions");

            migrationBuilder.DropColumn(
                name: "reset_demande_par_equipe",
                table: "drafts_champions");
        }
    }
}
