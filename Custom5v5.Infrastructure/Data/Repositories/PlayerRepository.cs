using Custom5v5.Application.DTOs.Players;
using Custom5v5.Application.Interfaces;
using Custom5v5.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Custom5v5.Infrastructure.Data.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly AppDbContext _db;

    public PlayerRepository(AppDbContext db) => _db = db;

    public async Task<List<PlayerDto>> GetAllAsync()
    {
        var players = await _db.Players.ToListAsync();
        return players.Select(p => ToDto(p)).ToList();
    }

    public Task<bool> ExistsByPuuidAsync(string puuid) =>
        _db.Players.AnyAsync(p => p.PUUID == puuid);

    public async Task UpdateRankAsync(int id, string? tier, int? division, int? lp)
    {
        var player = await _db.Players.FindAsync(id);
        if (player == null) return;

        player.RankTier = tier;
        player.RankDivision = division;
        player.LP = lp;

        await _db.SaveChangesAsync();
    }

    public async Task<PlayerDto> AddAsync(PlayerDto dto)
    {
        var player = new Player
        {
            Prenom = dto.Prenom,
            RiotId = dto.RiotId,
            PUUID = dto.PUUID,
            RankTier = dto.RankTier,
            RankDivision = dto.RankDivision,
            CreatedAt = DateTime.UtcNow,
            LP = dto.LP,
        };

        _db.Players.Add(player);
        await _db.SaveChangesAsync();

        dto.Id = player.Id;
        return dto;
    }

    // Lier un player à un user
    public async Task LinkUserAsync(int playerId, int userId)
    {
        var player = await _db.Players.FindAsync(playerId);
        if (player == null) throw new InvalidOperationException("Player not found");

        player.UserId = userId;
        await _db.SaveChangesAsync();
    }

    // Récupérer le player lié à un userId
    public async Task<PlayerDto?> GetByUserIdAsync(int userId)
    {
        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.UserId == userId);

        return player == null ? null : ToDto(player);
    }
    
    public async Task UpdateMainRoleAsync(int id, string? mainRole)
    {
        var player = await _db.Players.FindAsync(id);
        if (player == null) return;

        player.MainRole = mainRole;

        await _db.SaveChangesAsync();
    }

    public async Task UpdatePeakAsync(int playerId, string peakTier, int? peakDivision, string peakSeason, int peakLp)
    {
        var player = await _db.Players.FindAsync(playerId);
        if (player == null) return;

        player.PeakTier = peakTier;
        player.PeakDivision = peakDivision;
        player.PeakSeason = peakSeason;
        player.PeakLp = peakLp;

        await _db.SaveChangesAsync();
    }

    private static PlayerDto ToDto(Player p) => new()
    {
        Id = p.Id,
        Prenom = p.Prenom,
        RiotId = p.RiotId,
        PUUID = p.PUUID,
        RankTier = p.RankTier ?? "UNRANKED",  // ← null → UNRANKED
        RankDivision = p.RankDivision,
        MainRole = p.MainRole,
        LP = p.LP,
        PeakTier = p.PeakTier,
        PeakDivision = p.PeakDivision,
        PeakSeason = p.PeakSeason,
        PeakLp = p.PeakLp,
    };
}