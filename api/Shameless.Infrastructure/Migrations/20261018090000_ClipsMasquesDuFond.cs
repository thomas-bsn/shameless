using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Shameless.Infrastructure.Data;

#nullable disable

namespace Shameless.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261018090000_ClipsMasquesDuFond")]
public partial class ClipsMasquesDuFond : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<bool>(name: "masque_du_fond", table: "clips", type: "boolean", nullable: false, defaultValue: false);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "masque_du_fond", table: "clips");
}
