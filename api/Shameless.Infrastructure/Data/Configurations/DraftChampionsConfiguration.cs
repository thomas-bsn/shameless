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
        entity.Property(e => e.PauseCommenceLe).HasColumnType("timestamp without time zone");
        entity.Property(e => e.RepriseDemandeeLe).HasColumnType("timestamp without time zone");
        entity.Property(e => e.PauseParEquipe).HasMaxLength(1);
        entity.Property(e => e.RepriseDemandeeParEquipe).HasMaxLength(1);
        entity.Ignore(e => e.EnPause);
        entity.Ignore(e => e.PauseFinLe);
        entity.Ignore(e => e.RepriseExpireLe);
        entity.Property(e => e.Version).IsConcurrencyToken();
        entity.Ignore(e => e.EquipeBlue);
        entity.Ignore(e => e.EstTerminee);
        entity.Ignore(e => e.TourCourant);
        entity.Ignore(e => e.Echeance);

        // Une seule Draft de champions par game ; elle disparaît avec sa game (Série annulée).
        entity.HasOne(e => e.Game)
              .WithOne(g => g.DraftChampions)
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
