using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Data.Configurations;

/// <summary>La Draft de champions d'une game et ses tours joués.</summary>
public sealed class DraftChampionsConfiguration : IEntityTypeConfiguration<DraftChampions>
{
    public void Configure(EntityTypeBuilder<DraftChampions> entity)
    {
        entity.ToTable("drafts_champions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).UseIdentityByDefaultColumn();
        entity.Property(e => e.TourCommenceLe).HasColumnType("timestamp without time zone");
        entity.Property(e => e.Version).IsConcurrencyToken();
        entity.Ignore(e => e.EquipeBlue);
        entity.Ignore(e => e.EstTerminee);
        entity.Ignore(e => e.TourCourant);

        // Une seule Draft de champions par game ; elle disparaît avec sa game (Série annulée).
        entity.HasOne(e => e.Game)
              .WithOne()
              .HasForeignKey<DraftChampions>(e => e.GameId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(e => e.Actions)
              .WithOne()
              .HasForeignKey(a => a.DraftChampionsId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ActionDraftChampionsConfiguration : IEntityTypeConfiguration<ActionDraftChampions>
{
    public void Configure(EntityTypeBuilder<ActionDraftChampions> entity)
    {
        entity.ToTable("drafts_champions_actions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).UseIdentityByDefaultColumn();
        entity.Property(e => e.ChampionId).IsRequired();

        // Un tour ne se joue qu'une fois, et un champion n'est banni ou pick qu'une fois par game.
        entity.HasIndex(e => new { e.DraftChampionsId, e.Numero }).IsUnique();
        entity.HasIndex(e => new { e.DraftChampionsId, e.ChampionId }).IsUnique();
    }
}
