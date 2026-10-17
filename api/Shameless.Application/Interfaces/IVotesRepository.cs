using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

/// <summary>Les bulletins de vote MVP/DOFD et les Spectateurs présents de chaque Game.</summary>
public interface IVotesRepository
{
    Task<List<BulletinDeVote>> GetBulletinsAsync(int gameId);

    /// <summary>Les bulletins des Games données, toutes Games confondues.</summary>
    Task<List<BulletinDeVote>> GetBulletinsDesGamesAsync(IReadOnlyCollection<int> gameIds);

    Task<BulletinDeVote?> GetBulletinAsync(int gameId, long votantDiscordId);

    void Add(BulletinDeVote bulletin);

    void Remove(BulletinDeVote bulletin);

    Task<List<SpectateurDeGame>> GetSpectateursAsync(int gameId);

    Task<bool> EstSpectateurAsync(int gameId, long discordId);

    void AddSpectateur(SpectateurDeGame spectateur);

    void RemoveSpectateur(SpectateurDeGame spectateur);

    Task<bool> TrySaveChangesAsync();
}
