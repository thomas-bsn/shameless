using Shameless.Application.DTOs.Players;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Shameless.Infrastructure.Data.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;

    public PlayerRepository(AppDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<List<PlayerDto>> GetAllAsync()
    {
        var players = await _db.Players.ToListAsync();
        return players.Select(ToDto).ToList();
    }

    public async Task<PlayerDto?> GetDtoByIdAsync(int id)
    {
        var player = await _db.Players.FindAsync(id);
        return player == null ? null : ToDto(player);
    }

    public Task<bool> ExistsByPuuidAsync(string Puuid) =>
        _db.Players.AnyAsync(p => p.PUUID == Puuid);

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
        if (string.IsNullOrWhiteSpace(dto.Prenom))
            throw new ArgumentException("Prenom is required", nameof(dto.Prenom));
    
        if (string.IsNullOrWhiteSpace(dto.RiotId))
            throw new ArgumentException("RiotId is required", nameof(dto.RiotId));
    
        var player = new Player
        {
            Prenom = dto.Prenom, 
            RiotId = dto.RiotId,
            PUUID = dto.Puuid ?? throw new ArgumentException("Puuid is required"),
            RankTier = dto.RankTier,
            RankDivision = dto.RankDivision,
            MainRole = dto.MainRole,
            CreatedAt = _clock.GetUtcNow().UtcDateTime,
            LP = dto.Lp,
        };

        _db.Players.Add(player);
        await _db.SaveChangesAsync();

        dto.Id = player.Id;
        return dto;
    }

    public async Task LinkUserAsync(int playerId, int userId)
    {
        var player = await _db.Players.FindAsync(playerId);
        if (player == null) throw new InvalidOperationException("Player not found");

        // Un utilisateur n'est lié qu'à un seul joueur : on détache l'ancien.
        var previous = await _db.Players.Where(p => p.UserId == userId && p.Id != playerId).ToListAsync();
        foreach (var other in previous)
            other.UserId = null;

        player.UserId = userId;
        await _db.SaveChangesAsync();
    }

    public async Task<PlayerDto?> GetByUserIdAsync(int userId)
    {
        var player = await _db.Players.FirstOrDefaultAsync(p => p.UserId == userId);
        return player == null ? null : ToDto(player);
    }
    
    public async Task UpdateMainRoleAsync(int id, string? mainRole)
    {
        var player = await _db.Players.FindAsync(id);
        if (player == null) return;

        player.MainRole = mainRole;
        await _db.SaveChangesAsync();
    }

    public async Task UpdatePeakAsync(int playerId, string? peakTier, int? peakDivision, string? peakSeason, int peakLp)
    {
        var player = await _db.Players.FindAsync(playerId);
        if (player == null) return;

        player.PeakTier = peakTier;
        player.PeakDivision = peakDivision;
        player.PeakSeason = peakSeason;
        player.PeakLp = peakLp;

        await _db.SaveChangesAsync();
    }
    
    public async Task UpdateRiotIdAsync(int id, string riotId)
    {
        var player = await _db.Players.FindAsync(id);
        if (player == null) return;
        player.RiotId = riotId;
        await _db.SaveChangesAsync();
    }
    
    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();

    public async Task<Player?> GetByIdAsync(int id) =>
        await _db.Players.FindAsync(id);

    public async Task<List<Player>> GetByIdsAsync(List<int> ids) =>
        await _db.Players.Where(p => ids.Contains(p.Id)).ToListAsync();

    public async Task<List<Player>> GetAllEntitiesAsync() =>
        await _db.Players.ToListAsync();

    public async Task UpdateAsync(Player player)
    {
        _db.Players.Update(player);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateRangeAsync(List<Player> players)
    {
        _db.Players.UpdateRange(players);
        await _db.SaveChangesAsync();
    }

    private static PlayerDto ToDto(Player p) => new()
    {
        Id = p.Id,
        Prenom = p.Prenom,
        RiotId = p.RiotId,
        Puuid = p.PUUID,
        RankTier = p.RankTier ?? "UNRANKED",
        RankDivision = p.RankDivision,
        MainRole = p.MainRole,
        Lp = p.LP,
        PeakTier = p.PeakTier,
        PeakDivision = p.PeakDivision,
        PeakSeason = p.PeakSeason,
        PeakLp = p.PeakLp,
        CurrentCost = p.CurrentCost,
    };
}