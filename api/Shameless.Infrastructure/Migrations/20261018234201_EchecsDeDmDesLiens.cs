using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EchecsDeDmDesLiens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "echec_dm_le",
                table: "liens_acces",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "echec_dm_player_id",
                table: "liens_acces",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "echec_dm_raison",
                table: "liens_acces",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "echec_dm_le",
                table: "liens_acces");

            migrationBuilder.DropColumn(
                name: "echec_dm_player_id",
                table: "liens_acces");

            migrationBuilder.DropColumn(
                name: "echec_dm_raison",
                table: "liens_acces");
        }
    }
}
