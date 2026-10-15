using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Shameless.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CapitainesEtLiensAcces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "capitaine",
                table: "series_players",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "liens_capitaine_envoyes",
                table: "best_of_series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "liens_acces",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    series_id = table.Column<int>(type: "integer", nullable: false),
                    portee = table.Column<string>(type: "text", nullable: false),
                    equipe = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    secret = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_liens_acces", x => x.id);
                    table.ForeignKey(
                        name: "fk_liens_acces_best_of_series_series_id",
                        column: x => x.series_id,
                        principalTable: "best_of_series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_liens_acces_secret",
                table: "liens_acces",
                column: "secret",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_liens_acces_series_id_portee_equipe",
                table: "liens_acces",
                columns: new[] { "series_id", "portee", "equipe" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            // Les Séries existantes reçoivent leurs trois liens (ADR 0010) : deux Liens capitaine et le
            // Lien spectateur, chacun un secret de 64 caractères tiré de deux UUID aléatoires (244 bits).
            // Elles n'ont pas de capitaine : l'organisateur les désignera.
            migrationBuilder.Sql(@"
                INSERT INTO liens_acces (series_id, portee, equipe, secret)
                SELECT s.id, l.portee, l.equipe,
                       replace(gen_random_uuid()::text, '-', '') || replace(gen_random_uuid()::text, '-', '')
                FROM best_of_series s
                CROSS JOIN (VALUES ('capitaine', 'A'), ('capitaine', 'B'), ('spectateur', NULL)) AS l(portee, equipe);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "liens_acces");

            migrationBuilder.DropColumn(
                name: "capitaine",
                table: "series_players");

            migrationBuilder.DropColumn(
                name: "liens_capitaine_envoyes",
                table: "best_of_series");
        }
    }
}
