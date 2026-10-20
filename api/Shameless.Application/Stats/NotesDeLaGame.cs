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
        var equipes = game.Participants
            .Select(p => p.Side)
            .Distinct()
            .ToDictionary(side => side, side => EquipeDe(game, side));

        return game.Participants.ToDictionary(p => p.PlayerId, p =>
            Noter(game, p, ValeursEffectives.Stats(p), equipes[p.Side], duree));
    }

    /// <summary>La note d'un Joueur pour la Game, null s'il n'y a pas joué ou n'a pas de note.</summary>
    public static NoteDePerformance? DuJoueur(CustomGame game, int playerId) =>
        game.Participants.FirstOrDefault(p => p.PlayerId == playerId) is { } participant
            ? Noter(game, participant, ValeursEffectives.Stats(participant),
                EquipeDe(game, participant.Side), DureeDe(game))
            : null;

    /// <summary>La note du participant à partir de ses stats déjà lues ; null sans stats ou sans critère mesurable.</summary>
    internal static NoteDePerformance? Noter(
        CustomGame game, CustomGameParticipant participant, StatsDuParticipant? stats, TotauxDEquipe equipe, int? dureeSecondes)
    {
        var role = RoleJoueDe(game, participant, stats);
        var calculee = stats is null ? null : NoteDePerformance.Calculer(
            stats, role, equipe.Kills, dureeSecondes, equipe.Degats, equipe.Or, game.WinningTeam is { } vainqueur ? vainqueur == game.EquipeOn(participant.Side) : null);

        // La note sur les stats d'origine, pour l'afficher à côté d'une valeur corrigée.
        int? origine = participant.Corrections.Count == 0 ? calculee?.Note
            : ValeursEffectives.StatsOrigine(participant) is { } brutes
                ? NoteDePerformance.Calculer(brutes, RoleJoue.Determiner(brutes.Position, AutoroleDe(game, participant.PlayerId),
                    Autorole.RolePrincipal(participant.Player.MainRole)), equipe.Kills, dureeSecondes,
                    equipe.Degats, equipe.Or, game.WinningTeam is { } v ? v == game.EquipeOn(participant.Side) : null)?.Note
                : null;

        // Une note corrigée par un admin remplace la note calculée (sans composantes si rien n'était mesurable).
        return ValeursEffectives.NoteCorrigee(participant) is { } corrigee
            ? (calculee ?? new NoteDePerformance(corrigee, role.Role, role.Source, [])) with { Note = corrigee, NoteOrigine = origine }
            : calculee is null ? null : calculee with { NoteOrigine = origine };
    }

    /// <summary>Le rôle joué par le participant : position de la partie, sinon Autorole, sinon rôle principal.</summary>
    internal static RoleJoue RoleJoueDe(CustomGame game, CustomGameParticipant participant, StatsDuParticipant? stats) =>
        RoleJoue.Determiner(stats?.Position, AutoroleDe(game, participant.PlayerId), Autorole.RolePrincipal(participant.Player.MainRole));

    /// <summary>La durée de la game envoyée par le Companion, null sans données.</summary>
    internal static int? DureeDe(CustomGame game) => DonneesDuMatch.Lire(game.RiotMatchData)?.DurationSeconds;

    /// <summary>Ce que l'Équipe d'un Side a fait dans la game, base des critères relatifs de la note.</summary>
    /// <param name="Kills">La somme des kills connus de ses participants, null si aucun n'est connu.</param>
    /// <param name="Degats">Les dégâts aux champions de toute l'Équipe, null si un participant n'a pas cette stat.</param>
    /// <param name="Or">L'or gagné par toute l'Équipe, null si un participant n'a pas cette stat.</param>
    internal sealed record TotauxDEquipe(int? Kills, int? Degats, int? Or);

    /// <summary>Les totaux de l'Équipe sur ce Side ; dégâts et or demandent les stats de tous ses participants (une part sur un total partiel serait gonflée).</summary>
    internal static TotauxDEquipe EquipeDe(CustomGame game, string side)
    {
        var stats = game.Participants.Where(p => p.Side == side).Select(ValeursEffectives.Stats).ToList();
        int? Total(Func<StatsDuParticipant, int?> stat) =>
            stats.All(s => s is not null && stat(s) is not null) ? stats.Sum(s => stat(s!)!.Value) : null;
        return new TotauxDEquipe(KillsDeLEquipe(game, side), Total(s => s.DamageToChampions), Total(s => s.GoldEarned));
    }

    /// <summary>Les kills de l'Équipe sur ce Side : la somme des kills connus de ses participants, null si aucun n'est connu.</summary>
    internal static int? KillsDeLEquipe(CustomGame game, string side)
    {
        var kills = game.Participants
            .Where(p => p.Side == side)
            .Select(p => ValeursEffectives.Stats(p)?.Kills)
            .ToList();
        return kills.Any(k => k is not null) ? kills.Sum(k => k ?? 0) : null;
    }

    /// <summary>Le rôle attribué au Joueur par l'Autorole de la Série, null sans Autorole (ou sans <c>Series.Players</c> chargé).</summary>
    internal static Role? AutoroleDe(CustomGame game, int playerId) =>
        game.Series?.Players.FirstOrDefault(p => p.PlayerId == playerId)?.Role;
}
