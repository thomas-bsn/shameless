using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Data.Configurations;

/// <summary>Issue, options et composition courante d'une Série (la table legacy est mappée dans AppDbContext).</summary>
public sealed class SeriesConfiguration : IEntityTypeConfiguration<BestOfSeries>
{
    public void Configure(EntityTypeBuilder<BestOfSeries> entity)
    {
        entity.Property(e => e.Outcome)
              .HasColumnType("text")
              .HasConversion(
                  outcome => ToText(outcome),
                  value => FromText(value));
        entity.Property(e => e.WinningTeam).HasMaxLength(1);
        entity.Ignore(e => e.IsEnCours);
        entity.Ignore(e => e.WinsNeeded);
    }

    private static string ToText(SeriesOutcome outcome) => outcome switch
    {
        SeriesOutcome.Gagnee => "gagnee",
        SeriesOutcome.Nulle => "nulle",
        SeriesOutcome.Interrompue => "interrompue",
        _ => "en_cours"
    };

    private static SeriesOutcome FromText(string value) => value switch
    {
        "gagnee" => SeriesOutcome.Gagnee,
        "nulle" => SeriesOutcome.Nulle,
        "interrompue" => SeriesOutcome.Interrompue,
        _ => SeriesOutcome.EnCours
    };
}

public sealed class SeriesPlayerConfiguration : IEntityTypeConfiguration<SeriesPlayer>
{
    public void Configure(EntityTypeBuilder<SeriesPlayer> entity)
    {
        entity.ToTable("series_players");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).UseIdentityByDefaultColumn();
        entity.Property(e => e.Equipe).HasMaxLength(1).IsRequired();
        entity.Property(e => e.Role).HasColumnType("text").HasConversion(RoleText.Converter);

        entity.HasOne(e => e.Series)
              .WithMany(s => s.Players)
              .HasForeignKey(e => e.SeriesId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Player)
              .WithMany()
              .HasForeignKey(e => e.PlayerId)
              .OnDelete(DeleteBehavior.Cascade);

        // Un joueur ne figure qu'une fois dans une Série, donc dans une seule Équipe.
        entity.HasIndex(e => new { e.SeriesId, e.PlayerId }).IsUnique();
    }
}
