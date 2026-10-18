using Shameless.Application.Companion;
using Shameless.Application.Entities;
using Microsoft.EntityFrameworkCore;
using Shameless.Infrastructure.Data.Configurations;

namespace Shameless.Infrastructure.Data;

/// <summary>
/// Le modèle reflète le schéma réel de la base legacy (voir api/database/BASELINE.md) :
/// noms de contraintes, colonnes serial ou identity, valeurs par défaut.
/// </summary>
public class AppDbContext : DbContext
{
    private const string Timestamp = "timestamp without time zone";

    static AppDbContext()
    {
        // Les colonnes sont en timestamp sans fuseau : comportement legacy de Npgsql.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<User> Users => Set<User>();
    public DbSet<BestOfSeries> BestOfSeries => Set<BestOfSeries>();
    public DbSet<CustomGame> CustomGames => Set<CustomGame>();
    public DbSet<CustomGameParticipant> CustomGameParticipants => Set<CustomGameParticipant>();
    public DbSet<PlayerMonthlyCost> PlayerMonthlyCosts => Set<PlayerMonthlyCost>();
    public DbSet<Soiree> Soirees => Set<Soiree>();
    public DbSet<SeriesPlayer> SeriesPlayers => Set<SeriesPlayer>();
    public DbSet<PickerSession> PickerSessions => Set<PickerSession>();
    public DbSet<LienAcces> LiensAcces => Set<LienAcces>();
    public DbSet<DraftChampions> DraftsChampions => Set<DraftChampions>();
    public DbSet<RapportCompanion> RapportsCompanion => Set<RapportCompanion>();
    public DbSet<EvenementBot> EvenementsBot => Set<EvenementBot>();
    public DbSet<BulletinDeVote> BulletinsDeVote => Set<BulletinDeVote>();
    public DbSet<SpectateurDeGame> SpectateursDeGame => Set<SpectateurDeGame>();
    public DbSet<Pronostic> Pronostics => Set<Pronostic>();
    public DbSet<Clip> Clips => Set<Clip>();

    /// <summary>
    /// Les dates sont stockées en heure UTC sans fuseau : relues, elles repartent en
    /// <see cref="DateTimeKind.Utc"/>, et l'API les sérialise avec leur « Z ».
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<DateTimeUtcConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<DateTimeUtcConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(entity =>
        {
            entity.ToTable("players");
            entity.HasKey(e => e.Id).HasName("players_pkey");
            entity.Property(e => e.Id).UseSerialColumn();

            entity.Property(e => e.PUUID).HasColumnName("puuid").HasColumnType("character varying");
            entity.Property(e => e.Prenom).HasColumnType("text");
            entity.Property(e => e.RiotId).HasColumnType("text");
            entity.Property(e => e.RankTier).HasColumnType("text");
            entity.Property(e => e.FlexRankTier).HasColumnType("text");
            entity.Property(e => e.MainRole).HasColumnType("text");
            entity.Property(e => e.PeakTier).HasColumnType("text");
            entity.Property(e => e.PeakSeason).HasMaxLength(10);
            entity.Property(e => e.LP).HasColumnName("lp");

            entity.Property(e => e.CreatedAt)
                  .HasColumnType(Timestamp)
                  .HasDefaultValueSql("now()");

            entity.Property(e => e.PeakLp).HasDefaultValue(0);

            entity.Property(e => e.CurrentCost)
                  .HasColumnType("numeric(3,1)")
                  .HasDefaultValue(0m);

            entity.Property(e => e.IsPlacement).HasDefaultValue(false);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id).HasName("users_pk");
            entity.Property(e => e.Id).UseSerialColumn();
            entity.Property(e => e.DiscordUsername).HasColumnType("character varying");
            entity.Property(e => e.AvatarUrl).HasColumnType("character varying");
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(30).HasDefaultValue(UserRole.Utilisateur);
            entity.Property(e => e.PromotedBy).HasColumnType("character varying");
            entity.Property(e => e.PromotedAt).HasColumnType(Timestamp);

            entity.HasOne(u => u.Player)
                  .WithOne(p => p.User)
                  .HasForeignKey<Player>(p => p.UserId)
                  .HasConstraintName("players_user_id_fkey");
        });

        modelBuilder.Entity<BestOfSeries>(entity =>
        {
            entity.ToTable("best_of_series");
            entity.HasKey(e => e.Id).HasName("best_of_series_pkey");
            entity.Property(e => e.Id).UseSerialColumn();
            entity.Property(e => e.PlayedAt).HasColumnType(Timestamp);
            entity.Property(e => e.TeamAWins).HasDefaultValue(0);
            entity.Property(e => e.TeamBWins).HasDefaultValue(0);
        });

        modelBuilder.Entity<CustomGame>(entity =>
        {
            entity.ToTable("custom_games");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();

            entity.Property(e => e.PlayedAt).HasColumnType(Timestamp);
            entity.Property(e => e.TeamASide).HasMaxLength(10).IsRequired();
            entity.Property(e => e.WinningTeam).HasMaxLength(1);
            entity.Ignore(e => e.TeamBSide);
            entity.Ignore(e => e.RapportEnRevue);
            entity.Property(e => e.RiotMatchId).HasMaxLength(100);
            // Une partie Riot n'est rattachée qu'à une game (ADR 0004), même sous rapports simultanés.
            entity.HasIndex(e => e.RiotMatchId).IsUnique();
            entity.Property(e => e.RiotMatchData).HasColumnType("jsonb");
            // Jeton de concurrence : le résultat d'une game ne se saisit qu'une fois, même simultanément.
            entity.Property(e => e.Status).HasColumnType("text").HasDefaultValue("").IsConcurrencyToken();

            entity.Property(e => e.CreatedAt)
                  .HasColumnType(Timestamp)
                  .HasDefaultValueSql("now()");

            entity.Property(e => e.CompletedAt).HasColumnType(Timestamp);

            entity.HasOne(e => e.Series)
                  .WithMany(s => s.Games)
                  .HasForeignKey(e => e.SeriesId)
                  .HasConstraintName("custom_games_series_id_fkey");

            entity.HasIndex(e => e.PlayedAt);
        });

        modelBuilder.Entity<CustomGameParticipant>(entity =>
        {
            entity.ToTable("custom_game_participants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();

            entity.Property(e => e.Side).HasColumnName("team").HasMaxLength(10).IsRequired();
            entity.Ignore(e => e.HasWon);
            entity.Property(e => e.RiotParticipantData).HasColumnType("jsonb");
            entity.Property(e => e.ChampionId).HasMaxLength(RapportDeGame.LongueurMaxChampionId);

            entity.HasOne(e => e.CustomGame)
                  .WithMany(g => g.Participants)
                  .HasForeignKey(e => e.CustomGameId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Player)
                  .WithMany(p => p.CustomGameParticipations)
                  .HasForeignKey(e => e.PlayerId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.CustomGameId, e.PlayerId });
        });

        modelBuilder.Entity<PlayerMonthlyCost>(entity =>
        {
            entity.ToTable("player_monthly_costs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();

            entity.Property(e => e.Month).HasColumnType("date");
            entity.Property(e => e.FinalCost).HasColumnType("numeric(3,1)");
            entity.Property(e => e.CostDeDepart).HasColumnType("numeric(3,1)");
            entity.Property(e => e.SourceDuCostDeDepart).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.NoteMoyenne).HasColumnType("numeric(4,1)");
            entity.Property(e => e.BaseMMR).HasColumnName("base_mmr");

            entity.HasOne(e => e.Player)
                  .WithMany(p => p.MonthlyCosts)
                  .HasForeignKey(e => e.PlayerId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.PlayerId, e.Month }).IsUnique();
        });

        modelBuilder.ApplyConfiguration(new SoireeConfiguration());
        modelBuilder.ApplyConfiguration(new SeriesConfiguration());
        modelBuilder.ApplyConfiguration(new SeriesPlayerConfiguration());
        modelBuilder.ApplyConfiguration(new PickerSessionConfiguration());
        modelBuilder.ApplyConfiguration(new PickerSessionPlayerConfiguration());
        modelBuilder.ApplyConfiguration(new LienAccesConfiguration());
        modelBuilder.ApplyConfiguration(new DraftChampionsConfiguration());
        modelBuilder.ApplyConfiguration(new ActionDraftChampionsConfiguration());
        modelBuilder.ApplyConfiguration(new RapportCompanionConfiguration());
        modelBuilder.ApplyConfiguration(new EvenementBotConfiguration());
        modelBuilder.ApplyConfiguration(new BulletinDeVoteConfiguration());
        modelBuilder.ApplyConfiguration(new SpectateurDeGameConfiguration());
        modelBuilder.ApplyConfiguration(new PronosticConfiguration());
        modelBuilder.ApplyConfiguration(new ClipConfiguration());
    }
}
