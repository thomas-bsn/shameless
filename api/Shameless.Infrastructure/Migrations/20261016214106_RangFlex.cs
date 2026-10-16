using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RangFlex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "flex_lp",
                table: "players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "flex_rank_division",
                table: "players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "flex_rank_tier",
                table: "players",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "flex_lp",
                table: "players");

            migrationBuilder.DropColumn(
                name: "flex_rank_division",
                table: "players");

            migrationBuilder.DropColumn(
                name: "flex_rank_tier",
                table: "players");
        }
    }
}
