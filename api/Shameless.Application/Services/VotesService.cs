using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Shameless.Application.Votes;
using Shameless.Application.Discord;

namespace Shameless.Application.Services;

/// <summary>
/// Les votes MVP et DOFD d'une Game (ADR 0013 pour l'identité du bot qui les dépose). Les dix Joueurs
/// (par leur compte Discord lié) et les Spectateurs signalés présents en vocal peuvent voter, pendant
/// les 10 minutes qui suivent la fin de la Game, au plus un Joueur par titre, jamais pour eux-mêmes.
/// </summary>
public sealed class VotesService
{
    private readonly ICustomGameRepository _games;
    private readonly IVotesRepository _votes;
    private readonly TimeProvider _clock;

    public VotesService(ICustomGameRepository games, IVotesRepository votes, TimeProvider clock)
    {
        _games = games;
        _votes = votes;
        _clock = clock;
    }

    private DateTime Maintenant() => _clock.GetUtcNow().UtcDateTime;

    /// <summary>Les résultats des votes de la Game, null si elle n'existe pas.</summary>
    public async Task<ResultatsDesVotes?> GetResultatsAsync(int gameId) =>
        await _games.GetRecitAsync(gameId) is { } game
            ? ResultatsDesVotes.Calculer(game, await _votes.GetBulletinsAsync(gameId), Maintenant())
            : null;

    /// <summary>
    /// Remplace les Spectateurs présents en vocal pendant la Game par ceux que signale le bot.
    /// Refusé une fois les votes clos.
    /// </summary>
    public async Task<CommandResult<SpectateursDeLaGame>> SignalerSpectateursAsync(int gameId, IReadOnlyCollection<string> discordIds)
    {
        if (await _games.GetRecitAsync(gameId) is not { } game)
            return CommandResult<SpectateursDeLaGame>.NotFound("Cette game n'existe pas.");
        if (FenetreDeVote.Statut(game, Maintenant()) == FenetreDeVote.Clos)
            return CommandResult<SpectateursDeLaGame>.Conflict("Les votes de cette game sont clos.");

        var ids = new HashSet<long>();
        foreach (var discordId in discordIds)
        {
            if (DiscordId.Lire(discordId) is not { } id)
                return CommandResult<SpectateursDeLaGame>.Invalid($"« {discordId} » n'est pas un ID Discord.");
            ids.Add(id.Valeur);
        }

        var actuels = await _votes.GetSpectateursAsync(gameId);
        foreach (var spectateur in actuels.Where(s => !ids.Contains(s.DiscordId)))
            _votes.RemoveSpectateur(spectateur);
        foreach (var id in ids.Except(actuels.Select(s => s.DiscordId)))
            _votes.AddSpectateur(new SpectateurDeGame { GameId = gameId, DiscordId = id });

        if (!await _votes.TrySaveChangesAsync())
            return CommandResult<SpectateursDeLaGame>.Conflict("Les Spectateurs de cette game ont changé entre-temps, réessaie.");
        return CommandResult<SpectateursDeLaGame>.Ok(new SpectateursDeLaGame(
            gameId,
            ids.Order().Select(DiscordId.Texte).ToList()));
    }

    /// <summary>
    /// Dépose (ou remplace) le bulletin d'un votant : null pour s'abstenir sur un titre ; deux null
    /// retirent le bulletin.
    /// </summary>
    public async Task<CommandResult<BulletinDuVotant>> VoterAsync(int gameId, string votantDiscordId, int? mvpPlayerId, int? dofdPlayerId)
    {
        if (await _games.GetRecitAsync(gameId) is not { } game)
            return CommandResult<BulletinDuVotant>.NotFound("Cette game n'existe pas.");

        var maintenant = Maintenant();
        var statut = FenetreDeVote.Statut(game, maintenant);
        if (statut == FenetreDeVote.AVenir)
            return CommandResult<BulletinDuVotant>.Conflict("Les votes ouvrent à la fin de la game.");
        if (statut == FenetreDeVote.Clos)
            return CommandResult<BulletinDuVotant>.Conflict("Les votes de cette game sont clos.");

        if (DiscordId.Lire(votantDiscordId)?.Valeur is not { } votant)
            return CommandResult<BulletinDuVotant>.Invalid($"« {votantDiscordId} » n'est pas un ID Discord.");

        var estJoueur = game.Participants.Any(p => p.Player.User?.DiscordId == votant);
        if (!estJoueur && !await _votes.EstSpectateurAsync(gameId, votant))
            return CommandResult<BulletinDuVotant>.Invalid(
                "Seuls les dix Joueurs de la game et les Spectateurs présents en vocal peuvent voter.");

        var choix = new ChoixDesTitres(mvpPlayerId, dofdPlayerId);
        if (choix.TitreHorsDe(game.Participants.Select(p => p.PlayerId)) is { } horsDeLaGame)
            return CommandResult<BulletinDuVotant>.Invalid($"Le {horsDeLaGame} doit être l'un des dix Joueurs de la game.");
        if (choix.TitreParmi(game.Participants.Where(p => p.Player.User?.DiscordId == votant).Select(p => p.PlayerId)) is { } pourSoi)
            return CommandResult<BulletinDuVotant>.Invalid($"Tu ne peux pas voter pour toi-même ({pourSoi}).");

        Depot.RemplacerOuRetirer(
            await _votes.GetBulletinAsync(gameId, votant),
            vide: choix.Aucun,
            creer: () => new BulletinDeVote { GameId = gameId, VotantDiscordId = votant },
            ajouter: _votes.Add,
            retirer: _votes.Remove,
            remplir: bulletin =>
            {
                bulletin.MvpPlayerId = mvpPlayerId;
                bulletin.DofdPlayerId = dofdPlayerId;
                bulletin.VoteLe = maintenant;
            });

        if (!await _votes.TrySaveChangesAsync())
            return CommandResult<BulletinDuVotant>.Conflict("Ce bulletin a changé entre-temps, réessaie.");
        return CommandResult<BulletinDuVotant>.Ok(new BulletinDuVotant(gameId, votantDiscordId, mvpPlayerId, dofdPlayerId));
    }

    /// <summary>
    /// Les résultats des votes de chacune des Games données (chargées avec leurs participants et leurs
    /// Joueurs), par Game.
    /// </summary>
    public async Task<Dictionary<int, ResultatsDesVotes>> GetResultatsDesGamesAsync(IReadOnlyCollection<CustomGame> games)
    {
        var bulletins = (await _votes.GetBulletinsDesGamesAsync(games.Select(g => g.Id).ToList()))
            .ToLookup(b => b.GameId);
        var maintenant = Maintenant();
        return games.ToDictionary(g => g.Id, g => ResultatsDesVotes.Calculer(g, bulletins[g.Id], maintenant));
    }

    /// <summary>
    /// Les titres de MVP et de DOFD remportés par chaque Joueur dans les Games données (chargées avec
    /// leurs participants et leurs Joueurs) dont les votes sont clos, égalités comprises.
    /// </summary>
    public async Task<Dictionary<int, TitresDuJoueur>> GetTitresAsync(IReadOnlyCollection<CustomGame> games) =>
        TitresDuJoueur.Compter((await GetResultatsDesGamesAsync(games)).Values);

}

/// <summary>Les Spectateurs présents en vocal pendant une Game, par ID Discord (texte).</summary>
public sealed record SpectateursDeLaGame(int GameId, List<string> DiscordIds);

/// <summary>Le bulletin d'un votant : null pour une abstention sur ce titre.</summary>
public sealed record BulletinDuVotant(int GameId, string VotantDiscordId, int? MvpPlayerId, int? DofdPlayerId);
