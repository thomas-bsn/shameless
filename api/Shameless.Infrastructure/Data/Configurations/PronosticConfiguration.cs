using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Data.Configurations;

/// <summary>Les pronostics des Spectateurs : un par Spectateur et par Game d'une Série.</summary>
public sealed class PronosticConfiguration : IEntityTypeConfiguration<Pronostic>
{
    public void Configure(EntityTypeBuilder<Pronostic> entity)
    {
        entity.ToTable("pronostics", t =>
            t.HasCheckConstraint("ck_pronostics_equipe_gagnante", "equipe_gagnante IS NULL OR equipe_gagnante IN ('A', 'B')"));
        entity.HasKey(p => new { p.SeriesId, p.NumeroGame, p.PronostiqueurDiscordId });
        entity.Property(p => p.PronostiqueurNom).HasMaxLength(100);
        entity.Property(p => p.EquipeGagnante).HasMaxLength(1);
        entity.Property(p => p.PronostiqueLe).HasColumnType("timestamp without time zone");

        // Les pronostics disparaissent avec leur Série (ADR 0009).
        entity.HasOne(p => p.Series).WithMany().HasForeignKey(p => p.SeriesId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(p => p.Mvp).WithMany().HasForeignKey(p => p.MvpPlayerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(p => p.Dofd).WithMany().HasForeignKey(p => p.DofdPlayerId).OnDelete(DeleteBehavior.Restrict);
    }
}
