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
        var stats = game.Participants.ToDictionary(p => p.PlayerId, ValeursEffectives.Stats);
        return Calculer(game, stats, TotauxParSide(game, stats), DureeDe(game));
    }

    /// <summary>Les notes à partir de stats et totaux d'Équipe déjà lus (une seule lecture du JSON par participant).</summary>
    internal static IReadOnlyDictionary<int, NoteDePerformance?> Calculer(
        CustomGame game, IReadOnlyDictionary<int, StatsDuParticipant?> stats,
        IReadOnlyDictionary<string, TotauxDEquipe> totaux, int? dureeSecondes)
    {
        var origines = game.Participants.Select(p => p.Side).Distinct().ToDictionary(side => side, side => TotauxOrigine(game, side));
        return game.Participants.ToDictionary(p => p.PlayerId, p =>
            Noter(game, p, stats[p.PlayerId], totaux[p.Side], dureeSecondes, origines[p.Side]));
    }

    /// <summary>La note d'un Joueur pour la Game, null s'il n'y a pas joué ou n'a pas de note.</summary>
    public static NoteDePerformance? DuJoueur(CustomGame game, int playerId)
    {
        if (game.Participants.FirstOrDefault(p => p.PlayerId == playerId) is not { } participant) return null;
        var equipe = EquipeDe(game.Participants.Where(p => p.Side == participant.Side).Select(ValeursEffectives.Stats).ToList());
        return Noter(game, participant, ValeursEffectives.Stats(participant), equipe, DureeDe(game), TotauxOrigine(game, participant.Side));
    }

    /// <summary>
    /// La note du participant à partir de ses stats déjà lues ; null sans stats ou sans critère mesurable.
    /// <paramref name="equipeOrigine"/> : les totaux de l'Équipe sur les stats d'origine du Companion, null
    /// quand aucune stat de l'Équipe n'est corrigée (la note d'origine égale alors la note calculée).
    /// </summary>
    internal static NoteDePerformance? Noter(
        CustomGame game, CustomGameParticipant participant, StatsDuParticipant? stats, TotauxDEquipe equipe, int? dureeSecondes,
        TotauxDEquipe? equipeOrigine = null)
    {
        var role = RoleJoueDe(game, participant, stats);
        var calculee = stats is null ? null : NoteDePerformance.Calculer(
            stats, role, equipe.Kills, dureeSecondes, equipe.Degats, equipe.Or, Gagnee(game, participant));

        // La note sur les stats d'origine, pour l'afficher à côté d'une valeur corrigée : celles du joueur
        // comme les totaux de son Équipe.
        int? origine = equipeOrigine is null ? calculee?.Note
            : ValeursEffectives.StatsOrigine(participant) is { } brutes
                ? NoteDePerformance.Calculer(brutes, RoleJoueDe(game, participant, brutes), equipeOrigine.Kills, dureeSecondes,
                    equipeOrigine.Degats, equipeOrigine.Or, Gagnee(game, participant))?.Note
                : null;

        // Une note corrigée par un admin remplace la note calculée (sans composantes si rien n'était mesurable).
        return ValeursEffectives.NoteCorrigee(participant) is { } corrigee
            ? (calculee ?? new NoteDePerformance(corrigee, role.Role, role.Source, [])) with { Note = corrigee, NoteOrigine = origine }
            : calculee is null ? null : calculee with { NoteOrigine = origine };
    }

    /// <summary>Vrai si l'Équipe du participant a gagné, null si la game n'a pas de vainqueur.</summary>
    private static bool? Gagnee(CustomGame game, CustomGameParticipant participant) =>
        game.WinningTeam is { } vainqueur ? vainqueur == game.EquipeOn(participant.Side) : null;

    /// <summary>Le rôle joué par le participant : position de la partie, sinon Autorole, sinon rôle principal.</summary>
    internal static RoleJoue RoleJoueDe(CustomGame game, CustomGameParticipant participant, StatsDuParticipant? stats) =>
        RoleJoue.Determiner(stats?.Position, AutoroleDe(game, participant.PlayerId), Autorole.RolePrincipal(participant.Player.MainRole));

    /// <summary>La durée de la game envoyée par le Companion, null sans données.</summary>
    internal static int? DureeDe(CustomGame game) => DonneesDuMatch.Lire(game.RiotMatchData)?.DurationSeconds;

    /// <summary>
    /// Ce que l'Équipe d'un Side a fait dans la game, base des critères relatifs de la note et des parts de la page.
    /// Un seul calcul pour les deux : <see cref="EquipeDe"/>.
    /// </summary>
    /// <param name="Kills">La somme des kills connus de ses participants, null si aucun n'est connu.</param>
    /// <param name="Degats">Les dégâts aux champions de toute l'Équipe, null si un participant n'a pas cette stat.</param>
    /// <param name="Or">L'or gagné par toute l'Équipe, null si un participant n'a pas cette stat.</param>
    internal sealed record TotauxDEquipe(int? Kills, int? Degats, int? Or);

    /// <summary>Les totaux d'une Équipe à partir des stats de ses participants ; dégâts et or demandent celles de tous (une part sur un total partiel serait gonflée).</summary>
    internal static TotauxDEquipe EquipeDe(IReadOnlyCollection<StatsDuParticipant?> stats)
    {
        int? Total(Func<StatsDuParticipant, int?> stat) =>
            stats.All(s => s is not null && stat(s) is not null) ? stats.Sum(s => stat(s!)!.Value) : null;
        var kills = stats.Select(s => s?.Kills).ToList();
        return new TotauxDEquipe(
            kills.Any(k => k is not null) ? kills.Sum(k => k ?? 0) : null,
            Total(s => s.DamageToChampions), Total(s => s.GoldEarned));
    }

    /// <summary>Les totaux de chaque Side, sur les stats effectives déjà lues.</summary>
    internal static Dictionary<string, TotauxDEquipe> TotauxParSide(CustomGame game, IReadOnlyDictionary<int, StatsDuParticipant?> stats) =>
        game.Participants.Select(p => p.Side).Distinct().ToDictionary(
            side => side,
            side => EquipeDe(game.Participants.Where(p => p.Side == side).Select(p => stats[p.PlayerId]).ToList()));

    /// <summary>Les totaux de l'Équipe sur les stats d'origine du Companion ; null si aucune stat de ses participants n'est corrigée.</summary>
    private static TotauxDEquipe? TotauxOrigine(CustomGame game, string side)
    {
        var equipe = game.Participants.Where(p => p.Side == side).ToList();
        return equipe.Any(ValeursEffectives.StatsCorrigees)
            ? EquipeDe(equipe.Select(ValeursEffectives.StatsOrigine).ToList())
            : null;
    }

    /// <summary>Le rôle attribué au Joueur par l'Autorole de la Série, null sans Autorole (ou sans <c>Series.Players</c> chargé).</summary>
    internal static Role? AutoroleDe(CustomGame game, int playerId) =>
        game.Series?.Players.FirstOrDefault(p => p.PlayerId == playerId)?.Role;
}
