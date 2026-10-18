using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DiscordIdUniqueDesUtilisateurs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Une base qui contiendrait déjà des doublons ne doit pas empêcher le démarrage : sans doublon on crée
            // l'index unique, sinon on journalise et on ne fait rien (les doublons sont à fusionner à la main).
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM users GROUP BY discord_id HAVING count(*) > 1) THEN
                        RAISE WARNING 'users.discord_id : doublons présents, index unique ix_users_discord_id non créé.';
                    ELSE
                        CREATE UNIQUE INDEX IF NOT EXISTS ix_users_discord_id ON users (discord_id);
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_users_discord_id;");
        }
    }
}
