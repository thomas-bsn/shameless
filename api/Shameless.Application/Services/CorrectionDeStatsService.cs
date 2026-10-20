using System.Globalization;
using Shameless.Application.Companion;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Shameless.Application.Picker;

namespace Shameless.Application.Services;

/// <summary>
/// Les champs à corriger pour un joueur : un nombre pour une stat, un objet, une rune ou la note, un texte
/// pour le champion (id Data Dragon) et la voie (Top, Jungle, Mid, Adc, Support). Un champ absent ne change
/// pas. Le CS est le total (sbires et monstres neutres), la note va de 0 à 100.
/// </summary>
public sealed record DemandeDeCorrectionDeStats(IReadOnlyDictionary<ChampCorrigeable, object> Champs);

/// <summary>
/// La correction des stats d'un joueur dans une game terminée par un Administrateur (ADR 0009) : une
/// correction par champ et par joueur, stockée à part ; les données du Companion ne sont jamais
/// modifiées. La game porte « modifiée par un admin » et le cost du joueur est recalculé : sa note de
/// performance, donc son cost du mois, dépend des stats.
/// </summary>
public sealed class CorrectionDeStatsService
{
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
        if (!game.VainqueurOuCompositionCorriges && game.Participants.All(p => p.Corrections.Count == 0))
            return await EnregistrerAsync(game, playerId, change: true, marque: false);
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

    private async Task<CommandResult<CustomGame>> EnregistrerAsync(CustomGame game, int playerId, bool change, bool marque = true)
    {
        if (!change) return CommandResult<CustomGame>.Ok(game);

        game.ModifieeParAdminLe = marque ? _clock.GetUtcNow().UtcDateTime : null;
        if (!await _games.TrySaveChangesAsync())
            return CommandResult<CustomGame>.Conflict("Cette game vient d'être modifiée par une autre commande : rechargez-la et réessayez.");

        await _costs.RecalculatePlayerCost(playerId, DateOnly.FromDateTime(game.PlayedAt));
        return CommandResult<CustomGame>.Ok(game);
    }

    private static (List<(ChampCorrigeable Champ, string Valeur)> Valeurs, string? Detail) Valider(DemandeDeCorrectionDeStats d)
    {
        var valeurs = new List<(ChampCorrigeable, string)>();
        string? detail = null;

        foreach (var (champ, valeur) in d.Champs)
        {
            switch (valeur)
            {
                case string texte when champ == ChampCorrigeable.Champion:
                    var champion = texte.Trim();
                    if (champion.Length == 0 || champion.Length > RapportDeGame.LongueurMaxChampionId)
                        detail ??= "Le champion est un identifiant Data Dragon (ex. « LeeSin »).";
                    valeurs.Add((champ, champion));
                    break;
                case string texte when champ == ChampCorrigeable.Position:
                    if (Enum.TryParse<Role>(texte, ignoreCase: true, out var role) && Enum.IsDefined(role))
                        valeurs.Add((champ, role.ToString()));
                    else
                        detail ??= "La voie est Top, Jungle, Mid, Adc ou Support.";
                    break;
                case int nombre when !champ.Definition().Texte:
                    var (_, min, max) = champ.Definition();
                    if (nombre < min || nombre > max)
                        detail ??= $"Le champ « {champ.Nom()} » va de {min} à {max}.";
                    valeurs.Add((champ, nombre.ToString(CultureInfo.InvariantCulture)));
                    break;
                default:
                    detail ??= $"La valeur du champ « {champ.Nom()} » n'a pas le bon type.";
                    break;
            }
        }

        if (detail == null && valeurs.Count == 0)
            detail = "Indiquez au moins un champ à corriger.";
        return (valeurs, detail);
    }
}
