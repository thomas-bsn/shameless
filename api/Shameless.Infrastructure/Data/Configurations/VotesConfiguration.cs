using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Data.Configurations;

/// <summary>Les bulletins de vote MVP/DOFD : un par votant et par Game.</summary>
public sealed class BulletinDeVoteConfiguration : IEntityTypeConfiguration<BulletinDeVote>
{
    public void Configure(EntityTypeBuilder<BulletinDeVote> entity)
    {
        entity.ToTable("bulletins_de_vote");
        entity.HasKey(b => new { b.GameId, b.VotantDiscordId });
        entity.Property(b => b.VoteLe).HasColumnType("timestamp without time zone");

        // Les bulletins disparaissent avec leur Game (ADR 0009).
        entity.HasOne(b => b.Game).WithMany().HasForeignKey(b => b.GameId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Player>().WithMany().HasForeignKey(b => b.MvpPlayerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Player>().WithMany().HasForeignKey(b => b.DofdPlayerId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Les Spectateurs présents en vocal pendant une Game, signalés par le bot.</summary>
public sealed class SpectateurDeGameConfiguration : IEntityTypeConfiguration<SpectateurDeGame>
{
    public void Configure(EntityTypeBuilder<SpectateurDeGame> entity)
    {
        entity.ToTable("spectateurs_de_game");
        entity.HasKey(s => new { s.GameId, s.DiscordId });
        entity.HasOne(s => s.Game).WithMany().HasForeignKey(s => s.GameId).OnDelete(DeleteBehavior.Cascade);
    }
}
