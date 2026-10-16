using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CostMensuel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cost_de_depart",
                table: "player_monthly_costs",
                type: "numeric(3,1)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "note_moyenne",
                table: "player_monthly_costs",
                type: "numeric(4,1)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cost_de_depart",
                table: "player_monthly_costs");

            migrationBuilder.DropColumn(
                name: "note_moyenne",
                table: "player_monthly_costs");
        }
    }
}
