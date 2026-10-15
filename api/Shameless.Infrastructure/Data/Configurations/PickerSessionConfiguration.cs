using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Data.Configurations;

public sealed class PickerSessionConfiguration : IEntityTypeConfiguration<PickerSession>
{
    public void Configure(EntityTypeBuilder<PickerSession> entity)
    {
        entity.ToTable("picker_sessions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).UseIdentityByDefaultColumn();

        entity.Property(e => e.Methode).HasColumnType("text").HasConversion(LowercaseText<PickerMethode>());
        entity.Property(e => e.Variante).HasColumnType("text").HasConversion(LowercaseText<RouletteVariante>());
        entity.Property(e => e.PremierPick).HasMaxLength(1);
        entity.Ignore(e => e.CanValidate);
        entity.Ignore(e => e.Picks);
        entity.Ignore(e => e.EquipeAuTrait);

        // Au plus une session de Picker par Soirée.
        entity.HasOne(e => e.Soiree)
              .WithOne(s => s.Picker)
              .HasForeignKey<PickerSession>(e => e.SoireeId)
              .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(e => e.SoireeId).IsUnique();

        entity.HasMany(e => e.Players)
              .WithOne()
              .HasForeignKey(p => p.PickerSessionId)
              .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>Une valeur d'enum en texte minuscule (« roulette », « equilibree »…).</summary>
    private static ValueConverter<TEnum, string> LowercaseText<TEnum>() where TEnum : struct, Enum =>
        new(value => value.ToString().ToLowerInvariant(), text => Enum.Parse<TEnum>(text, true));
}

public sealed class PickerSessionPlayerConfiguration : IEntityTypeConfiguration<PickerSessionPlayer>
{
    public void Configure(EntityTypeBuilder<PickerSessionPlayer> entity)
    {
        entity.ToTable("picker_session_players");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).UseIdentityByDefaultColumn();
        entity.Property(e => e.Equipe).HasMaxLength(1);
        entity.Property(e => e.Role).HasColumnType("text").HasConversion(RoleText.Converter);

        entity.HasOne(e => e.Player)
              .WithMany()
              .HasForeignKey(e => e.PlayerId)
              .OnDelete(DeleteBehavior.Cascade);

        // Dix joueurs distincts.
        entity.HasIndex(e => new { e.PickerSessionId, e.PlayerId }).IsUnique();
    }
}
