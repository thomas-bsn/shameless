using Shameless.Application.Entities;
using Shameless.Application.Picker;
using Shameless.Application.Stats;

namespace Shameless.Application.Joueurs;

/// <summary>
/// Les stats agrégées d'un Joueur sur des Games terminées. Le winrate compte toutes les Games ; chaque
/// autre stat ne compte que les Games où le Companion l'a fournie, et vaut null si aucune ne la fournit.
/// </summary>
/// <param name="Winrate">Pourcentage de victoires (0 à 100, une décimale), null sans game.</param>
/// <param name="Kda">(kills + assists) / morts (au moins 1) sur les games concernées, deux décimales.</param>
/// <param name="KillsMoyens">Kills moyens par game, une décimale (de même pour morts et assists).</param>
/// <param name="CsParMinute">CS total / minutes jouées, sur les games avec CS et durée, une décimale.</param>
/// <param name="ParticipationAuxKills">Kills et assists / kills de l'Équipe, en pourcentage (une décimale).</param>
/// <param name="DegatsMoyens">Dégâts infligés aux champions, moyenne par game, arrondie.</param>
/// <param name="DegatsParMinute">Dégâts aux champions / minutes jouées, sur les games avec durée, arrondis.</param>
/// <param name="NoteMoyenne">Moyenne des notes de performance (sur 100), une décimale ; null sans note.</param>
/// <param name="GamesNotees">Nombre de games avec une note de performance.</param>
public sealed record StatsDeCustoms(
    int GamesJouees,
    int Victoires,
    int Defaites,
    double? Winrate,
    double? Kda,
    double? KillsMoyens,
    double? MortsMoyennes,
    double? AssistsMoyens,
    double? CsParMinute,
    double? ParticipationAuxKills,
    int? DegatsMoyens,
    int? DegatsParMinute,
    double? NoteMoyenne,
    int GamesNotees)
{
    internal static StatsDeCustoms Calculer(IReadOnlyCollection<PartieDuJoueur> parties)
    {
        var victoires = parties.Count(p => p.Victoire);
        var stats = parties.Where(p => p.Stats is not null).ToList();

        var kda = stats.Where(p => p.Stats is { Kills: not null, Deaths: not null, Assists: not null }).ToList();
        var cs = stats.Where(p => p.Minutes is not null && (p.Stats!.MinionsKilled is not null || p.Stats.NeutralMinionsKilled is not null)).ToList();
        var kp = stats.Where(p => p.Stats is { Kills: not null, Assists: not null } && p.KillsDeLEquipe > 0).ToList();
        var degats = stats.Where(p => p.Stats!.DamageToChampions is not null).ToList();
        var dpm = degats.Where(p => p.Minutes is not null).ToList();
        var notes = parties.Where(p => p.Performance is not null).Select(p => p.Performance!.Note).ToList();

        return new StatsDeCustoms(
            parties.Count,
            victoires,
            parties.Count - victoires,
            parties.Count == 0 ? null : Math.Round(100.0 * victoires / parties.Count, 1),
            kda.Count == 0 ? null : Math.Round(
                (double)kda.Sum(p => p.Stats!.Kills!.Value + p.Stats.Assists!.Value) / Math.Max(1, kda.Sum(p => p.Stats!.Deaths!.Value)), 2),
            Moyenne(stats, s => s.Kills),
            Moyenne(stats, s => s.Deaths),
            Moyenne(stats, s => s.Assists),
            cs.Count == 0 ? null : Math.Round(
                cs.Sum(p => (p.Stats!.MinionsKilled ?? 0) + (p.Stats.NeutralMinionsKilled ?? 0)) / cs.Sum(p => p.Minutes!.Value), 1),
            kp.Count == 0 ? null : Math.Round(
                100.0 * kp.Sum(p => p.Stats!.Kills!.Value + p.Stats.Assists!.Value) / kp.Sum(p => p.KillsDeLEquipe!.Value), 1),
            degats.Count == 0 ? null : (int)Math.Round(degats.Average(p => (double)p.Stats!.DamageToChampions!.Value), MidpointRounding.AwayFromZero),
            dpm.Count == 0 ? null : (int)Math.Round(dpm.Sum(p => p.Stats!.DamageToChampions!.Value) / dpm.Sum(p => p.Minutes!.Value), MidpointRounding.AwayFromZero),
            notes.Count == 0 ? null : Math.Round(notes.Average(), 1),
            notes.Count);
    }

    private static double? Moyenne(IEnumerable<PartieDuJoueur> parties, Func<StatsDuParticipant, int?> stat)
    {
        var valeurs = parties.Select(p => stat(p.Stats!)).OfType<int>().ToList();
        return valeurs.Count == 0 ? null : Math.Round(valeurs.Average(), 1);
    }
}

/// <summary>La participation d'un Joueur à une Game terminée, lue une fois pour toutes les stats de la fiche.</summary>
internal sealed record PartieDuJoueur(
    CustomGame Game,
    CustomGameParticipant Participant,
    bool Victoire,
    StatsDuParticipant? Stats,
    int? DureeSecondes,
    int? KillsDeLEquipe,
    Role? Role,
    NoteDePerformance? Performance)
{
    /// <summary>La participation du Joueur à la Game, null s'il n'y a pas joué.</summary>
    public static PartieDuJoueur? De(CustomGame game, int playerId)
    {
        if (game.Participants.FirstOrDefault(p => p.PlayerId == playerId) is not { } participant) return null;

        var stats = StatsDuParticipant.Lire(participant.RiotParticipantData);
        var duree = DonneesDuMatch.Lire(game.RiotMatchData)?.DurationSeconds;
        var killsDesCoequipiers = game.Participants
            .Where(p => p.Side == participant.Side)
            .Select(p => StatsDuParticipant.Lire(p.RiotParticipantData)?.Kills)
            .ToList();
        int? killsDeLEquipe = killsDesCoequipiers.Any(k => k is not null) ? killsDesCoequipiers.Sum(k => k ?? 0) : null;

        // Rôle joué : la position de la partie, sinon le rôle de l'Autorole ; le rôle principal SoloQ
        // n'est qu'un repli de la note, pas un rôle réellement joué.
        var role = stats?.Position ?? game.Series?.Players.FirstOrDefault(p => p.PlayerId == playerId)?.Role;

        return new PartieDuJoueur(
            game,
            participant,
            participant.HasWon,
            stats,
            duree is > 0 ? duree : null,
            killsDeLEquipe,
            role,
            NotesDeLaGame.DuJoueur(game, playerId));
    }

    /// <summary>Les minutes jouées, null sans durée connue.</summary>
    public double? Minutes => DureeSecondes / 60.0;

    public GameDeLaFiche EnLigne() => new(
        Game.Id,
        Game.SeriesId,
        Game.PlayedAt,
        Game.EquipeOn(Participant.Side),
        Victoire,
        DureeSecondes,
        Participant.ChampionId,
        Role,
        Stats,
        Performance);
}
