using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Data.Configurations;

/// <summary>Les liens d'accès d'une Série (ADR 0010).</summary>
public sealed class LienAccesConfiguration : IEntityTypeConfiguration<LienAcces>
{
    public void Configure(EntityTypeBuilder<LienAcces> entity)
    {
        entity.ToTable("liens_acces");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).UseIdentityByDefaultColumn();
        entity.Property(e => e.Portee)
              .HasColumnType("text")
              .HasConversion(
                  portee => portee == PorteeLien.Capitaine ? "capitaine" : "spectateur",
                  value => value == "capitaine" ? PorteeLien.Capitaine : PorteeLien.Spectateur);
        entity.Property(e => e.Equipe).HasMaxLength(1);
        entity.Property(e => e.Secret).IsRequired();
        entity.Property(e => e.RejointLe).HasColumnType("timestamp without time zone");

        entity.HasOne(e => e.Series)
              .WithMany(s => s.Liens)
              .HasForeignKey(e => e.SeriesId)
              .OnDelete(DeleteBehavior.Cascade);

        // Un lien se résout par son secret.
        entity.HasIndex(e => e.Secret).IsUnique();
        // Un Lien capitaine par Équipe et un Lien spectateur par Série.
        entity.HasIndex(e => new { e.SeriesId, e.Portee, e.Equipe }).IsUnique().AreNullsDistinct(false);
    }
}
