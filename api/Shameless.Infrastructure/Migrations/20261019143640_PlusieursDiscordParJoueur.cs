using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlusieursDiscordParJoueur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "players_user_id_fkey",
                table: "players");

            migrationBuilder.DropIndex(
                name: "ix_players_user_id",
                table: "players");

            migrationBuilder.AddColumn<int>(
                name: "player_id",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "player_linked_at",
                table: "users",
                type: "timestamp without time zone",
                nullable: true);

            // Les liens existants passent côté Utilisateur. La date du lien n'a jamais été enregistrée : maintenant.
            // players.user_id reste le Discord principal, déjà exact puisqu'il n'y avait qu'un lien par Joueur.
            migrationBuilder.Sql(@"
                UPDATE users u SET player_id = p.id, player_linked_at = (now() AT TIME ZONE 'utc')
                FROM players p WHERE p.user_id = u.id;");

            migrationBuilder.CreateIndex(
                name: "ix_users_player_id",
                table: "users",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_players_user_id",
                table: "players",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "players_user_id_fkey",
                table: "players",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "users_player_id_fkey",
                table: "users",
                column: "player_id",
                principalTable: "players",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "players_user_id_fkey",
                table: "players");

            migrationBuilder.DropForeignKey(
                name: "users_player_id_fkey",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_player_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_players_user_id",
                table: "players");

            migrationBuilder.DropColumn(
                name: "player_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "player_linked_at",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "ix_players_user_id",
                table: "players",
                column: "user_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "players_user_id_fkey",
                table: "players",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id");
        }
    }
}
