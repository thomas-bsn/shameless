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
        var duree = DureeDe(game);
        var killsParSide = game.Participants
            .Select(p => p.Side)
            .Distinct()
            .ToDictionary(side => side, side => KillsDeLEquipe(game, side));

        return game.Participants.ToDictionary(p => p.PlayerId, p =>
            Noter(game, p, StatsDuParticipant.Lire(p.RiotParticipantData), killsParSide[p.Side], duree));
    }

    /// <summary>La note d'un Joueur pour la Game, null s'il n'y a pas joué ou n'a pas de note.</summary>
    public static NoteDePerformance? DuJoueur(CustomGame game, int playerId) =>
        game.Participants.FirstOrDefault(p => p.PlayerId == playerId) is { } participant
            ? Noter(game, participant, StatsDuParticipant.Lire(participant.RiotParticipantData),
                KillsDeLEquipe(game, participant.Side), DureeDe(game))
            : null;

    /// <summary>La note du participant à partir de ses stats déjà lues ; null sans stats ou sans critère mesurable.</summary>
    internal static NoteDePerformance? Noter(
        CustomGame game, CustomGameParticipant participant, StatsDuParticipant? stats, int? killsDeLEquipe, int? dureeSecondes) =>
        stats is null
            ? null
            : NoteDePerformance.Calculer(
                stats,
                RoleJoue.Determiner(stats.Position, AutoroleDe(game, participant.PlayerId), Autorole.RolePrincipal(participant.Player.MainRole)),
                killsDeLEquipe,
                dureeSecondes);

    /// <summary>La durée de la game envoyée par le Companion, null sans données.</summary>
    internal static int? DureeDe(CustomGame game) => DonneesDuMatch.Lire(game.RiotMatchData)?.DurationSeconds;

    /// <summary>Les kills de l'Équipe sur ce Side : la somme des kills connus de ses participants, null si aucun n'est connu.</summary>
    internal static int? KillsDeLEquipe(CustomGame game, string side)
    {
        var kills = game.Participants
            .Where(p => p.Side == side)
            .Select(p => StatsDuParticipant.Lire(p.RiotParticipantData)?.Kills)
            .ToList();
        return kills.Any(k => k is not null) ? kills.Sum(k => k ?? 0) : null;
    }

    /// <summary>Le rôle attribué au Joueur par l'Autorole de la Série, null sans Autorole (ou sans <c>Series.Players</c> chargé).</summary>
    internal static Role? AutoroleDe(CustomGame game, int playerId) =>
        game.Series?.Players.FirstOrDefault(p => p.PlayerId == playerId)?.Role;
}
