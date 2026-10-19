using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VainqueurOuCompositionCorriges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Une base qui a servi une ancienne version du projet peut déjà porter cette colonne.
            migrationBuilder.Sql(
                "ALTER TABLE custom_games ADD COLUMN IF NOT EXISTS vainqueur_ou_composition_corriges boolean NOT NULL DEFAULT false;");
            // Une game déjà marquée sans aucune correction de stat a été corrigée sur son vainqueur ou sa composition.
            migrationBuilder.Sql("""
                UPDATE custom_games g SET vainqueur_ou_composition_corriges = true
                WHERE g.modifiee_par_admin_le IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM corrections_de_stats c
                      JOIN custom_game_participants p ON p.id = c.custom_game_participant_id
                      WHERE p.custom_game_id = g.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE custom_games DROP COLUMN IF EXISTS vainqueur_ou_composition_corriges;");
        }
    }
}
