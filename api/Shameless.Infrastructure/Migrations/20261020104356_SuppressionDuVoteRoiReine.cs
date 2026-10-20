using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SuppressionDuVoteRoiReine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Les votes et pronostics de Roi/Reine sont supprimés ; un bulletin ou un pronostic qui ne
            // portait que ce choix serait vide : il est retiré avec lui.
            migrationBuilder.Sql("DELETE FROM bulletins_de_vote WHERE dofd_player_id IS NULL;");
            migrationBuilder.Sql("DELETE FROM pronostics WHERE equipe_gagnante IS NULL AND dofd_player_id IS NULL;");

            migrationBuilder.DropForeignKey(
                name: "fk_bulletins_de_vote_players_mvp_player_id",
                table: "bulletins_de_vote");

            migrationBuilder.DropForeignKey(
                name: "fk_pronostics_players_mvp_player_id",
                table: "pronostics");

            migrationBuilder.DropIndex(
                name: "ix_pronostics_mvp_player_id",
                table: "pronostics");

            migrationBuilder.DropIndex(
                name: "ix_bulletins_de_vote_mvp_player_id",
                table: "bulletins_de_vote");

            migrationBuilder.DropColumn(
                name: "mvp_player_id",
                table: "pronostics");

            migrationBuilder.DropColumn(
                name: "mvp_player_id",
                table: "bulletins_de_vote");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "mvp_player_id",
                table: "pronostics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "mvp_player_id",
                table: "bulletins_de_vote",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_pronostics_mvp_player_id",
                table: "pronostics",
                column: "mvp_player_id");

            migrationBuilder.CreateIndex(
                name: "ix_bulletins_de_vote_mvp_player_id",
                table: "bulletins_de_vote",
                column: "mvp_player_id");

            migrationBuilder.AddForeignKey(
                name: "fk_bulletins_de_vote_players_mvp_player_id",
                table: "bulletins_de_vote",
                column: "mvp_player_id",
                principalTable: "players",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pronostics_players_mvp_player_id",
                table: "pronostics",
                column: "mvp_player_id",
                principalTable: "players",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
