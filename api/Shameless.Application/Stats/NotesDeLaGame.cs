using Shameless.Application.Entities;
using Shameless.Application.Picker;

namespace Shameless.Application.Stats;

/// <summary>
/// Les notes de performance des participants d'une Game, à partir de ses données brutes du Companion.
/// Point d'entrée des stats, de la fiche Joueur et du cost mensuel pour obtenir la note d'un Joueur.
/// </summary>
/// <remarks>
/// Pour le rôle de repli de l'Autorole, la game doit être chargée avec <c>Series.Players</c> ; sinon
/// le rôle principal du Joueur (<c>Participants.Player</c>) sert de repli.
/// </remarks>
public static class NotesDeLaGame
{
    /// <summary>La note de chaque participant, par PlayerId ; null pour un participant sans stats ou sans critère mesurable.</summary>
    public static IReadOnlyDictionary<int, NoteDePerformance?> Calculer(CustomGame game)
    {
        var duree = DonneesDuMatch.Lire(game.RiotMatchData)?.DurationSeconds;
        var stats = game.Participants.ToDictionary(p => p, p => StatsDuParticipant.Lire(p.RiotParticipantData));
        var killsParSide = stats
            .GroupBy(s => s.Key.Side)
            .ToDictionary(g => g.Key, g => g.Any(s => s.Value?.Kills is not null) ? g.Sum(s => s.Value?.Kills ?? 0) : (int?)null);

        return stats.ToDictionary(s => s.Key.PlayerId, s => s.Value is null
            ? null
            : NoteDePerformance.Calculer(s.Value, RoleDe(game, s.Key, s.Value), killsParSide[s.Key.Side], duree));
    }

    /// <summary>La note d'un Joueur pour la Game, null s'il n'y a pas joué ou n'a pas de note.</summary>
    public static NoteDePerformance? DuJoueur(CustomGame game, int playerId) =>
        Calculer(game).GetValueOrDefault(playerId);

    private static RoleJoue RoleDe(CustomGame game, CustomGameParticipant participant, StatsDuParticipant stats) =>
        RoleJoue.Determiner(
            stats.Position,
            game.Series?.Players.FirstOrDefault(p => p.PlayerId == participant.PlayerId)?.Role,
            Autorole.RolePrincipal(participant.Player.MainRole));
}
