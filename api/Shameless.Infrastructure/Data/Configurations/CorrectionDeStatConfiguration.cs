using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Data.Configurations;

/// <summary>Les corrections de stats d'un participant : une par champ, stockées à part du JSON du Companion.</summary>
public sealed class CorrectionDeStatConfiguration : IEntityTypeConfiguration<CorrectionDeStat>
{
    public void Configure(EntityTypeBuilder<CorrectionDeStat> entity)
    {
        entity.ToTable("corrections_de_stats");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).UseIdentityByDefaultColumn();
        entity.Property(e => e.Champ).HasConversion<string>().HasMaxLength(30);
        entity.Property(e => e.Valeur).HasMaxLength(60).IsRequired();

        // Une correction par champ et par participant ; elle disparaît avec lui (game supprimée, joueur retiré).
        entity.HasIndex(e => new { e.CustomGameParticipantId, e.Champ }).IsUnique();
        entity.HasOne(e => e.CustomGameParticipant)
              .WithMany(p => p.Corrections)
              .HasForeignKey(e => e.CustomGameParticipantId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
