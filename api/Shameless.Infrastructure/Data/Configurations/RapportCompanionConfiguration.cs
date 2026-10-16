using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Data.Configurations;

/// <summary>Les rapports du Companion mis en revue.</summary>
public sealed class RapportCompanionConfiguration : IEntityTypeConfiguration<RapportCompanion>
{
    public void Configure(EntityTypeBuilder<RapportCompanion> entity)
    {
        entity.ToTable("rapports_companion");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).UseIdentityByDefaultColumn();
        entity.Property(e => e.RiotMatchId).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Contenu).HasColumnType("jsonb").IsRequired();
        entity.Property(e => e.Statut)
              .HasColumnType("text")
              .HasConversion(
                  statut => statut == StatutRapport.EnRevue ? "en_revue"
                      : statut == StatutRapport.Accepte ? "accepte" : "rejete",
                  value => value == "en_revue" ? StatutRapport.EnRevue
                      : value == "accepte" ? StatutRapport.Accepte : StatutRapport.Rejete)
              .IsConcurrencyToken();
        entity.Property(e => e.RecuLe).HasColumnType("timestamp without time zone");
        entity.Property(e => e.DecideLe).HasColumnType("timestamp without time zone");

        // Le rapport disparaît avec sa game (Série interrompue ou annulée, ADR 0009).
        entity.HasOne(e => e.Game)
              .WithMany(g => g.Rapports)
              .HasForeignKey(e => e.GameId)
              .OnDelete(DeleteBehavior.Cascade);

        // Au plus un rapport en revue par game, même reçus simultanément.
        entity.HasIndex(e => e.GameId)
              .IsUnique()
              .HasFilter("statut = 'en_revue'")
              .HasDatabaseName("ix_rapports_companion_game_id_en_revue");
    }
}
