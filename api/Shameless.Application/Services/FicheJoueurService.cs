using Shameless.Application.Interfaces;
using Shameless.Application.Joueurs;

namespace Shameless.Application.Services;

/// <summary>Lecture de la fiche d'un Joueur.</summary>
public class FicheJoueurService
{
    private readonly IPlayerRepository _players;
    private readonly ICustomGameRepository _games;

    public FicheJoueurService(IPlayerRepository players, ICustomGameRepository games)
    {
        _players = players;
        _games = games;
    }

    /// <summary>La fiche du Joueur, null s'il n'existe pas.</summary>
    public async Task<FicheJoueur?> GetAsync(int playerId)
    {
        if (await _players.GetWithUserAsync(playerId) is not { } joueur) return null;
        return FicheJoueur.Calculer(joueur, await _games.GetCompletedGamesOfPlayerAsync(playerId));
    }
}
