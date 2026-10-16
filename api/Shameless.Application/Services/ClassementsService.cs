using Shameless.Application.Classements;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

/// <summary>Lecture des classements de l'Instance : customs 5v5 et SoloQ, toujours distincts.</summary>
public class ClassementsService
{
    private readonly IPlayerRepository _players;
    private readonly ICustomGameRepository _games;

    public ClassementsService(IPlayerRepository players, ICustomGameRepository games)
    {
        _players = players;
        _games = games;
    }

    public async Task<Classement5v5> Get5v5Async() =>
        Classement5v5.Calculer(await _players.GetAllEntitiesAsync(), await _games.GetCompletedParticipationsAsync());

    public async Task<ClassementSoloQ> GetSoloQAsync() =>
        ClassementSoloQ.Calculer(await _players.GetAllEntitiesAsync());
}
