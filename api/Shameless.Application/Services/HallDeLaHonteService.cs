using Shameless.Application.Interfaces;
using Shameless.Application.Votes;

namespace Shameless.Application.Services;

/// <summary>Lecture du Hall de la Honte : les titres de DOFD de toutes les Games terminées, cumulés.</summary>
public sealed class HallDeLaHonteService
{
    private readonly ICustomGameRepository _games;
    private readonly VotesService _votes;

    public HallDeLaHonteService(ICustomGameRepository games, VotesService votes)
    {
        _games = games;
        _votes = votes;
    }

    public async Task<HallDeLaHonte> GetAsync()
    {
        var games = await _games.GetCompletedGamesWithPlayersAsync();
        var titres = await _votes.GetTitresAsync(games);
        var joueurs = games
            .SelectMany(g => g.Participants)
            .Select(p => p.Player)
            .DistinctBy(p => p.Id);
        return HallDeLaHonte.Classer(joueurs, titres);
    }
}
