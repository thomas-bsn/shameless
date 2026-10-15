using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Custom5v5.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMainRoleToPlayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "main_role",
                table: "players",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "main_role",
                table: "players");
        }
    }
}
