using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Data.Configurations;

/// <summary>L'outbox des webhooks vers le bot Discord (ADR 0013).</summary>
public sealed class EvenementBotConfiguration : IEntityTypeConfiguration<EvenementBot>
{
    public void Configure(EntityTypeBuilder<EvenementBot> entity)
    {
        entity.ToTable("evenements_bot");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).UseIdentityByDefaultColumn();
        entity.Property(e => e.Type)
              .HasColumnType("text")
              .HasConversion(
                  type => type == TypeEvenementBot.TeamsFormed ? "teams_formed"
                      : type == TypeEvenementBot.CaptainLink ? "captain_link"
                      : type == TypeEvenementBot.SpectatorLink ? "spectator_link"
                      : type == TypeEvenementBot.GameStarted ? "game_started" : "game_completed",
                  value => value == "teams_formed" ? TypeEvenementBot.TeamsFormed
                      : value == "captain_link" ? TypeEvenementBot.CaptainLink
                      : value == "spectator_link" ? TypeEvenementBot.SpectatorLink
                      : value == "game_started" ? TypeEvenementBot.GameStarted : TypeEvenementBot.GameCompleted);
        entity.Property(e => e.Equipe).HasMaxLength(1);
        entity.Property(e => e.Statut)
              .HasColumnType("text")
              .HasConversion(
                  statut => statut == StatutEvenementBot.Livre ? "livre"
                      : statut == StatutEvenementBot.Abandonne ? "abandonne" : "a_livrer",
                  value => value == "livre" ? StatutEvenementBot.Livre
                      : value == "abandonne" ? StatutEvenementBot.Abandonne : StatutEvenementBot.ALivrer);
        entity.Property(e => e.OccurredAt).HasColumnType("timestamp without time zone");
        entity.Property(e => e.ProchaineTentative).HasColumnType("timestamp without time zone");
        entity.Property(e => e.LivreLe).HasColumnType("timestamp without time zone");
        entity.Property(e => e.DerniereErreur).HasMaxLength(500);
        entity.Ignore(e => e.EventId);
        entity.Ignore(e => e.TypeDuContrat);

        // L'événement disparaît avec son sujet : Série annulée, game en attente supprimée (ADR 0009).
        entity.HasOne(e => e.Series).WithMany().HasForeignKey(e => e.SeriesId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Game).WithMany().HasForeignKey(e => e.GameId).OnDelete(DeleteBehavior.Cascade);

        entity.ToTable(t => t.HasCheckConstraint("ck_evenements_bot_sujet", "(series_id IS NULL) <> (game_id IS NULL)"));

        // Un seul événement par type et par sujet (et par Équipe, pour un Lien capitaine) : l'event_id en dépend.
        entity.HasIndex(e => new { e.Type, e.SeriesId }).IsUnique().HasFilter("series_id IS NOT NULL AND equipe IS NULL");
        entity.HasIndex(e => new { e.Type, e.SeriesId, e.Equipe }).IsUnique().HasFilter("series_id IS NOT NULL AND equipe IS NOT NULL");
        entity.HasIndex(e => new { e.Type, e.GameId }).IsUnique().HasFilter("game_id IS NOT NULL");

        entity.HasIndex(e => new { e.Statut, e.ProchaineTentative });
    }
}
