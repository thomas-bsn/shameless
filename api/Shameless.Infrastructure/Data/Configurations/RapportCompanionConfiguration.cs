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
                  statut => ToText(statut),
                  value => FromText(value))
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

        // Un identifiant Riot n'est gardé qu'une fois : le même rapport renvoyé (reprise réseau,
        // plusieurs Companions) ne crée jamais un second rapport, qu'il soit en revue ou rejeté.
        entity.HasIndex(e => e.RiotMatchId).IsUnique();
    }

    private static string ToText(StatutRapport statut) => statut switch
    {
        StatutRapport.Accepte => "accepte",
        StatutRapport.Rejete => "rejete",
        _ => "en_revue"
    };

    private static StatutRapport FromText(string value) => value switch
    {
        "accepte" => StatutRapport.Accepte,
        "rejete" => StatutRapport.Rejete,
        _ => StatutRapport.EnRevue
    };
}
