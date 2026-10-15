using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Custom5v5.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBestOfSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS best_of_series (
                    id SERIAL PRIMARY KEY,
                    played_at TIMESTAMP NOT NULL,
                    format INTEGER NOT NULL
                );
            ");

            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name='custom_games' AND column_name='series_id'
                    ) THEN
                        ALTER TABLE custom_games ADD series_id INTEGER REFERENCES best_of_series(id);
                    END IF;
                END $$;
            ");

                    migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_custom_games_series_id ON custom_games(series_id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_custom_games_best_of_series_series_id",
                table: "custom_games");

            migrationBuilder.DropTable(
                name: "best_of_series");

            migrationBuilder.DropIndex(
                name: "ix_custom_games_series_id",
                table: "custom_games");

            migrationBuilder.DropColumn(
                name: "series_id",
                table: "custom_games");
        }
    }
}
