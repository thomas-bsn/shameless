using System.Globalization;
using Shameless.Application.Companion;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Shameless.Application.Picker;

namespace Shameless.Application.Services;

/// <summary>
/// Les champs à corriger pour un joueur ; les champs absents (null) ne changent pas. Le CS est le
/// total (sbires et monstres neutres), la voie un rôle (Top, Jungle, Mid, Adc, Support), la note va de 0 à 100.
/// </summary>
public sealed record DemandeDeCorrectionDeStats(
    string? Champion = null,
    string? Position = null,
    int? Kills = null,
    int? Deaths = null,
    int? Assists = null,
    int? Cs = null,
    int? GoldEarned = null,
    int? DamageToChampions = null,
    int? VisionScore = null,
    int? Note = null);

/// <summary>
/// La correction des stats d'un joueur dans une game terminée par un Administrateur (ADR 0009) : une
/// correction par champ et par joueur, stockée à part ; les données du Companion ne sont jamais
/// modifiées. La game porte « modifiée par un admin » et le cost du joueur est recalculé : sa note de
/// performance, donc son cost du mois, dépend des stats.
/// </summary>
public sealed class CorrectionDeStatsService
{
    private const int StatMax = 1_000_000;

    private readonly ICustomGameRepository _games;
    private readonly CostCalculationService _costs;
    private readonly TimeProvider _clock;

    public CorrectionDeStatsService(ICustomGameRepository games, CostCalculationService costs, TimeProvider clock)
    {
        _games = games;
        _costs = costs;
        _clock = clock;
    }

    /// <summary>Pose les corrections demandées (remplace celle d'un champ déjà corrigé).</summary>
    public async Task<CommandResult<CustomGame>> CorrigerAsync(int gameId, int playerId, DemandeDeCorrectionDeStats demande)
    {
        var (valeurs, detail) = Valider(demande);
        if (detail != null) return CommandResult<CustomGame>.Invalid(detail);

        var charge = await ChargerAsync(gameId, playerId);
        if (charge.Refus != null) return charge.Refus;
        var (game, participant) = (charge.Game!, charge.Participant!);

        var change = false;
        foreach (var (champ, valeur) in valeurs)
        {
            var existante = participant.Corrections.FirstOrDefault(c => c.Champ == champ);
            if (existante == null)
            {
                participant.Corrections.Add(new CorrectionDeStat { Champ = champ, Valeur = valeur });
                change = true;
            }
            else if (existante.Valeur != valeur)
            {
                existante.Valeur = valeur;
                change = true;
            }
        }
        return await EnregistrerAsync(game, playerId, change);
    }

    /// <summary>Annule la correction d'un champ : la valeur d'origine du Companion est rétablie.</summary>
    public async Task<CommandResult<CustomGame>> AnnulerAsync(int gameId, int playerId, string? nomDuChamp)
    {
        if (!ChampCorrigeableExtensions.TryParse(nomDuChamp, out var champ))
            return CommandResult<CustomGame>.Invalid($"« {nomDuChamp} » n'est pas un champ corrigeable.");

        var charge = await ChargerAsync(gameId, playerId);
        if (charge.Refus != null) return charge.Refus;
        var (game, participant) = (charge.Game!, charge.Participant!);

        if (participant.Corrections.FirstOrDefault(c => c.Champ == champ) is not { } correction)
            return CommandResult<CustomGame>.NotFound("Ce champ n'a pas de correction à annuler.");
        participant.Corrections.Remove(correction);
        return await EnregistrerAsync(game, playerId, change: true);
    }

    private async Task<(CustomGame? Game, CustomGameParticipant? Participant, CommandResult<CustomGame>? Refus)> ChargerAsync(
        int gameId, int playerId)
    {
        var game = await _games.GetByIdWithParticipantsAsync(gameId);
        if (game == null)
            return (null, null, CommandResult<CustomGame>.NotFound("Cette game n'existe pas."));
        if (game.Status != GameStatuses.Completed)
            return (null, null, CommandResult<CustomGame>.Conflict("Cette game n'est pas terminée : saisissez son résultat."));
        var participant = game.Participants.SingleOrDefault(p => p.PlayerId == playerId);
        if (participant == null)
            return (null, null, CommandResult<CustomGame>.NotFound("Ce joueur n'a pas joué cette game."));
        return (game, participant, null);
    }

    private async Task<CommandResult<CustomGame>> EnregistrerAsync(CustomGame game, int playerId, bool change)
    {
        if (!change) return CommandResult<CustomGame>.Ok(game);

        game.ModifieeParAdminLe = _clock.GetUtcNow().UtcDateTime;
        if (!await _games.TrySaveChangesAsync())
            return CommandResult<CustomGame>.Conflict("Cette game vient d'être modifiée par une autre commande : rechargez-la et réessayez.");

        await _costs.RecalculatePlayerCost(playerId, DateOnly.FromDateTime(game.PlayedAt));
        return CommandResult<CustomGame>.Ok(game);
    }

    private static (List<(ChampCorrigeable Champ, string Valeur)> Valeurs, string? Detail) Valider(DemandeDeCorrectionDeStats d)
    {
        var valeurs = new List<(ChampCorrigeable, string)>();
        string? detail = null;

        void Stat(ChampCorrigeable champ, int? valeur, int max = StatMax)
        {
            if (valeur is not { } v) return;
            if (v < 0 || v > max)
                detail ??= $"Le champ « {champ.Nom()} » va de 0 à {max}.";
            valeurs.Add((champ, v.ToString(CultureInfo.InvariantCulture)));
        }

        if (d.Champion != null)
        {
            var champion = d.Champion.Trim();
            if (champion.Length == 0 || champion.Length > RapportDeGame.LongueurMaxChampionId)
                detail ??= "Le champion est un identifiant Data Dragon (ex. « LeeSin »).";
            valeurs.Add((ChampCorrigeable.Champion, champion));
        }
        if (d.Position != null)
        {
            if (Enum.TryParse<Role>(d.Position, ignoreCase: true, out var role) && Enum.IsDefined(role))
                valeurs.Add((ChampCorrigeable.Position, role.ToString()));
            else
                detail ??= "La voie est Top, Jungle, Mid, Adc ou Support.";
        }
        Stat(ChampCorrigeable.Kills, d.Kills);
        Stat(ChampCorrigeable.Deaths, d.Deaths);
        Stat(ChampCorrigeable.Assists, d.Assists);
        Stat(ChampCorrigeable.Cs, d.Cs);
        Stat(ChampCorrigeable.GoldEarned, d.GoldEarned);
        Stat(ChampCorrigeable.DamageToChampions, d.DamageToChampions);
        Stat(ChampCorrigeable.VisionScore, d.VisionScore);
        Stat(ChampCorrigeable.Note, d.Note, max: 100);

        if (detail == null && valeurs.Count == 0)
            detail = "Indiquez au moins un champ à corriger.";
        return (valeurs, detail);
    }
}
