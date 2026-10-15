using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Data.Configurations;

public sealed class SoireeConfiguration : IEntityTypeConfiguration<Soiree>
{
    public void Configure(EntityTypeBuilder<Soiree> entity)
    {
        entity.ToTable("soirees");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).UseIdentityByDefaultColumn();

        entity.Property(e => e.Status)
              .HasColumnType("text")
              .HasConversion(
                  status => status == SoireeStatus.Open ? "open" : "closed",
                  value => value == "open" ? SoireeStatus.Open : SoireeStatus.Closed);
        entity.Property(e => e.OpenedAt).HasColumnType("timestamp without time zone");
        entity.Property(e => e.ClosedAt).HasColumnType("timestamp without time zone");

        // Au plus une Soirée ouverte par instance, garanti par la base.
        entity.HasIndex(e => e.Status)
              .IsUnique()
              .HasFilter("status = 'open'")
              .HasDatabaseName("ix_soirees_une_seule_ouverte");

        entity.HasMany(e => e.Series)
              .WithOne(s => s.Soiree)
              .HasForeignKey(s => s.SoireeId)
              .OnDelete(DeleteBehavior.Restrict);
    }
}
