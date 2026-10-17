using Shameless.Application.Bot;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Shameless.Application.Pronostics;
using Shameless.Application.Discord;

namespace Shameless.Application.Services;

/// <summary>
/// Les pronostics des Spectateurs (déposés par le bot, ADR 0013) : sur la prochaine Game d'une Série
/// en cours, tant qu'elle n'est pas lancée — son lancement est son début et verrouille les pronostics.
/// Un Spectateur choisit, chacun facultatif, l'Équipe gagnante, le MVP et le DOFD parmi la composition
/// courante. Les Joueurs de la Série ne pronostiquent pas. Ni points ni classement.
/// </summary>
public sealed class PronosticsService
{
    private const int LongueurMaxNom = 100;

    private readonly IPronosticsRepository _pronostics;
    private readonly ICustomGameRepository _games;
    private readonly VotesService _votes;
    private readonly TimeProvider _clock;

    public PronosticsService(IPronosticsRepository pronostics, ICustomGameRepository games, VotesService votes, TimeProvider clock)
    {
        _pronostics = pronostics;
        _games = games;
        _votes = votes;
        _clock = clock;
    }

    /// <summary>
    /// Ce que le bot propose de pronostiquer pour la Série : la prochaine Game si les pronostics sont
    /// ouverts, et la composition courante des deux Équipes. Null si la Série n'existe pas.
    /// </summary>
    public async Task<PronosticsDeLaSerie?> GetOuvertsAsync(int seriesId)
    {
        if (await _pronostics.GetSerieAsync(seriesId) is not { } series) return null;
        List<JoueurDuBot> Equipe(string equipe) => series.PlayersOf(equipe)
            .OrderBy(p => p.Id)
            .Select(p => new JoueurDuBot(p.PlayerId, p.Player.Prenom, GameDuBot.DiscordIdDe(p.Player)))
            .ToList();
        return new PronosticsDeLaSerie(series.Id, series.Format, ProchaineGameOuverte(series), Equipe(Equipes.A), Equipe(Equipes.B));
    }

    /// <summary>
    /// Dépose (ou remplace) le pronostic d'un Spectateur sur la Game <paramref name="gameNumber"/> de la
    /// Série ; trois choix null le retirent.
    /// </summary>
    public async Task<CommandResult<PronosticDuSpectateur>> PronostiquerAsync(
        int seriesId, string pronostiqueurDiscordId, string? pronostiqueurNom, int gameNumber,
        string? equipeGagnante, int? mvpPlayerId, int? dofdPlayerId)
    {
        if (await _pronostics.GetSerieAsync(seriesId) is not { } series)
            return CommandResult<PronosticDuSpectateur>.NotFound("Cette Série n'existe pas.");
        if (!series.IsEnCours)
            return CommandResult<PronosticDuSpectateur>.Conflict("Cette Série est terminée : plus de pronostic possible.");

        var prochaine = ProchaineGameOuverte(series);
        if (gameNumber <= series.Games.Count || prochaine is null)
            return CommandResult<PronosticDuSpectateur>.Conflict("Cette game est lancée : ses pronostics sont verrouillés.");
        if (gameNumber != prochaine)
            return CommandResult<PronosticDuSpectateur>.Invalid($"On ne pronostique que la prochaine game de la Série (game {prochaine}).");

        if (DiscordId.Lire(pronostiqueurDiscordId)?.Valeur is not { } pronostiqueur)
            return CommandResult<PronosticDuSpectateur>.Invalid($"« {pronostiqueurDiscordId} » n'est pas un ID Discord.");
        if (series.Players.Any(p => p.Player.User?.DiscordId == pronostiqueur))
            return CommandResult<PronosticDuSpectateur>.Invalid("Les pronostics sont réservés aux Spectateurs : les Joueurs de la Série ne pronostiquent pas.");

        if (equipeGagnante != null && !Equipes.IsValid(equipeGagnante))
            return CommandResult<PronosticDuSpectateur>.Invalid("L'Équipe gagnante est « A » ou « B ».");
        foreach (var (choix, titre) in new[] { (mvpPlayerId, "MVP"), (dofdPlayerId, "DOFD") })
            if (choix is { } playerId && series.Players.All(p => p.PlayerId != playerId))
                return CommandResult<PronosticDuSpectateur>.Invalid($"Le {titre} pronostiqué doit être l'un des dix Joueurs de la Série.");

        var nom = string.IsNullOrWhiteSpace(pronostiqueurNom) ? null : pronostiqueurNom.Trim();
        if (nom is { Length: > LongueurMaxNom }) nom = nom[..LongueurMaxNom];

        var pronostic = await _pronostics.GetAsync(seriesId, gameNumber, pronostiqueur);
        if (equipeGagnante is null && mvpPlayerId is null && dofdPlayerId is null)
        {
            if (pronostic != null) _pronostics.Remove(pronostic);
        }
        else
        {
            if (pronostic == null)
            {
                pronostic = new Pronostic { SeriesId = seriesId, NumeroGame = gameNumber, PronostiqueurDiscordId = pronostiqueur };
                _pronostics.Add(pronostic);
            }
            pronostic.PronostiqueurNom = nom;
            pronostic.EquipeGagnante = equipeGagnante;
            pronostic.MvpPlayerId = mvpPlayerId;
            pronostic.DofdPlayerId = dofdPlayerId;
            pronostic.PronostiqueLe = _clock.GetUtcNow().UtcDateTime;
        }

        if (!await _pronostics.TrySaveChangesAsync())
            return CommandResult<PronosticDuSpectateur>.Conflict("Ce pronostic a changé entre-temps, réessaie.");
        return CommandResult<PronosticDuSpectateur>.Ok(new PronosticDuSpectateur(
            seriesId, gameNumber, pronostiqueurDiscordId, nom, equipeGagnante, mvpPlayerId, dofdPlayerId));
    }

    /// <summary>Les pronostics évalués d'une Game, null si elle n'existe pas.</summary>
    public async Task<PronosticsDeLaGame?> GetDeLaGameAsync(int gameId)
    {
        if (await _games.GetRecitAsync(gameId) is not { } game) return null;
        var votes = (await _votes.GetResultatsAsync(gameId))!;
        var pronostics = game.SeriesId is { } seriesId
            ? await _pronostics.GetDeLaGameAsync(seriesId, game.NumeroDansLaSerie)
            : [];
        return PronosticsDeLaGame.Evaluer(game, pronostics, votes);
    }

    /// <summary>Le numéro de la prochaine Game si la Série est en cours sans game lancée en attente, sinon null.</summary>
    private static int? ProchaineGameOuverte(BestOfSeries series) =>
        series.IsEnCours && series.Games.All(g => g.Status == GameStatuses.Completed)
            ? series.Games.Count + 1
            : null;
}

/// <summary>Les pronostics ouverts d'une Série, pour le bot.</summary>
/// <param name="BestOf">Le format de la Série (1, 2, 3 ou 5).</param>
/// <param name="GameNumber">La prochaine Game, ouverte aux pronostics ; null s'ils sont verrouillés (game lancée) ou la Série terminée.</param>
/// <param name="TeamA">La composition courante de l'Équipe A, celle de la prochaine Game.</param>
/// <param name="TeamB">La composition courante de l'Équipe B.</param>
public sealed record PronosticsDeLaSerie(int SeriesId, int BestOf, int? GameNumber, List<JoueurDuBot> TeamA, List<JoueurDuBot> TeamB);

/// <summary>Le pronostic déposé par un Spectateur ; un choix null n'a pas été fait.</summary>
public sealed record PronosticDuSpectateur(
    int SeriesId,
    int GameNumber,
    string PronostiqueurDiscordId,
    string? PronostiqueurNom,
    string? EquipeGagnante,
    int? MvpPlayerId,
    int? DofdPlayerId);
