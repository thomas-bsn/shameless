using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PickerSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "picker_sessions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    soiree_id = table.Column<int>(type: "integer", nullable: false),
                    methode = table.Column<string>(type: "text", nullable: true),
                    variante = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_picker_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_picker_sessions_soirees_soiree_id",
                        column: x => x.soiree_id,
                        principalTable: "soirees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "picker_session_players",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    picker_session_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    equipe = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_picker_session_players", x => x.id);
                    table.ForeignKey(
                        name: "fk_picker_session_players_picker_sessions_picker_session_id",
                        column: x => x.picker_session_id,
                        principalTable: "picker_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_picker_session_players_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_picker_session_players_picker_session_id_player_id",
                table: "picker_session_players",
                columns: new[] { "picker_session_id", "player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_picker_session_players_player_id",
                table: "picker_session_players",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_picker_sessions_soiree_id",
                table: "picker_sessions",
                column: "soiree_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "picker_session_players");

            migrationBuilder.DropTable(
                name: "picker_sessions");
        }
    }
}
