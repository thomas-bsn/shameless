using Custom5v5.Application.Entities;
using Microsoft.EntityFrameworkCore;

namespace Custom5v5.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // DbSets existants
    public DbSet<Player> Players => Set<Player>();
    public DbSet<User> Users => Set<User>();
    
    public DbSet<CustomGame> CustomGames => Set<CustomGame>();
    public DbSet<CustomGameParticipant> CustomGameParticipants => Set<CustomGameParticipant>();
    public DbSet<PlayerMonthlyCost> PlayerMonthlyCosts => Set<PlayerMonthlyCost>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Player
        modelBuilder.Entity<Player>(entity =>
        {
            entity.ToTable("players");
            entity.HasKey(e => e.Id);
            
            // ✅ AJOUT : Nouveaux champs
            entity.Property(e => e.CurrentCost)
                  .HasColumnName("current_cost")
                  .HasColumnType("numeric(3,1)")
                  .HasDefaultValue(0m);
            
            entity.Property(e => e.IsPlacement)
                  .HasColumnName("is_placement")
                  .HasDefaultValue(false);
        });

        // User
        modelBuilder.Entity<User>().ToTable("users");

        // Relation User <-> Player
        modelBuilder.Entity<User>()
            .HasOne(u => u.Player)
            .WithOne(p => p.User)
            .HasForeignKey<Player>(p => p.UserId);
        
        // ✅ AJOUT : CustomGame
        modelBuilder.Entity<CustomGame>(entity =>
        {
            entity.ToTable("custom_games");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                  .HasColumnName("id")
                  .ValueGeneratedOnAdd(); // Auto-increment
            
            entity.Property(e => e.PlayedAt)
                  .HasColumnName("played_at")
                  .IsRequired();
            
            entity.Property(e => e.Winner)
                  .HasColumnName("winner")
                  .HasMaxLength(10)
                  .IsRequired();
            
            entity.Property(e => e.RiotMatchId)
                  .HasColumnName("riot_match_id")
                  .HasMaxLength(100);
            
            entity.Property(e => e.RiotMatchData)
                  .HasColumnName("riot_match_data")
                  .HasColumnType("jsonb");
            
            entity.Property(e => e.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");
            
            entity.HasIndex(e => e.PlayedAt);
        });
        
        // ✅ AJOUT : CustomGameParticipant
        modelBuilder.Entity<CustomGameParticipant>(entity =>
        {
            entity.ToTable("custom_game_participants");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                  .HasColumnName("id")
                  .ValueGeneratedOnAdd();
            
            entity.Property(e => e.CustomGameId)
                  .HasColumnName("custom_game_id");
            
            entity.Property(e => e.PlayerId)
                  .HasColumnName("player_id");
            
            entity.Property(e => e.Team)
                  .HasColumnName("team")
                  .HasMaxLength(10)
                  .IsRequired();
            
            entity.Property(e => e.RiotParticipantData)
                  .HasColumnName("riot_participant_data")
                  .HasColumnType("jsonb");
            
            // Relations
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
        
        // ✅ AJOUT : PlayerMonthlyCost
        modelBuilder.Entity<PlayerMonthlyCost>(entity =>
        {
            entity.ToTable("player_monthly_costs");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                  .HasColumnName("id")
                  .ValueGeneratedOnAdd();
            
            entity.Property(e => e.PlayerId)
                  .HasColumnName("player_id");
            
            entity.Property(e => e.Month)
                  .HasColumnName("month")
                  .HasColumnType("date")
                  .IsRequired();
            
            entity.Property(e => e.FinalCost)
                  .HasColumnName("final_cost")
                  .HasColumnType("numeric(3,1)");
            
            entity.Property(e => e.BaseMMR)
                  .HasColumnName("base_mmr");
            
            entity.Property(e => e.WinrateAdjustment)
                  .HasColumnName("winrate_adjustment");
            
            entity.Property(e => e.GamesPlayed)
                  .HasColumnName("games_played");
            
            entity.Property(e => e.Wins)
                  .HasColumnName("wins");
            
            // Relation
            entity.HasOne(e => e.Player)
                  .WithMany(p => p.MonthlyCosts)
                  .HasForeignKey(e => e.PlayerId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // Index unique
            entity.HasIndex(e => new { e.PlayerId, e.Month })
                  .IsUnique();
        });
    }
}