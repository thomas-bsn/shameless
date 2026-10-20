using Microsoft.Extensions.Options;
using Shameless.Application.Clips;
using Shameless.Application.Interfaces;
using Shameless.Application.Joueurs;
using Shameless.Application.Votes;

namespace Shameless.Application.Services;

/// <summary>Lecture de la fiche d'un Joueur.</summary>
public class FicheJoueurService
{
    private readonly IPlayerRepository _players;
    private readonly ICustomGameRepository _games;
    private readonly VotesService _votes;

    private readonly TimeZoneInfo _fuseau;

    public FicheJoueurService(IPlayerRepository players, ICustomGameRepository games, VotesService votes, IOptions<InstanceOptions> instance)
    {
        _fuseau = instance.Value.Fuseau();
        _players = players;
        _games = games;
        _votes = votes;
    }

    /// <summary>La fiche du Joueur, null s'il n'existe pas.</summary>
    public async Task<FicheJoueur?> GetAsync(int playerId, FiltreDeGames? filtre = null)
    {
        if (await _players.GetWithUserAsync(playerId) is not { } joueur) return null;
        var games = await _games.GetCompletedGamesOfPlayerAsync(playerId);
        return FicheJoueur.Calculer(joueur, games, await _votes.GetResultatsDesGamesAsync(games), filtre, _fuseau);
    }
}
