using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PickerDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "premier_pick",
                table: "picker_sessions",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "capitaine",
                table: "picker_session_players",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "pick_order",
                table: "picker_session_players",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "premier_pick",
                table: "picker_sessions");

            migrationBuilder.DropColumn(
                name: "capitaine",
                table: "picker_session_players");

            migrationBuilder.DropColumn(
                name: "pick_order",
                table: "picker_session_players");
        }
    }
}
