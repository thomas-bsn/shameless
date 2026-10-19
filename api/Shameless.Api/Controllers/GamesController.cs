using Microsoft.AspNetCore.Authorization;
using Shameless.Api.Auth;
using Shameless.Api.Contracts.Matches;
using Shameless.Api.Extensions;
using Shameless.Api.Models.Requests.Companion;
using Shameless.Api.Models.Responses.Companion;
using Shameless.Api.Models.Responses.Game;
using Shameless.Application.DTOs.Match;
using Shameless.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Shameless.Api.Controllers;

/// <summary>
/// Les games. Une game se crée dans une Série (<c>POST /series/{id}/games</c>).
/// </summary>
[ApiController]
[Route("games")]
public sealed class GamesController : ControllerBase
{
    private readonly CustomGameService _gameService;
    private readonly SeriesService _seriesService;

    private readonly CorrectionDeGameService _corrections;

    private readonly CorrectionDeStatsService _correctionsDeStats;

    private readonly RapportsCompanionService _rapports;

    public GamesController(
        CustomGameService gameService, SeriesService seriesService, CorrectionDeGameService corrections,
        CorrectionDeStatsService correctionsDeStats, RapportsCompanionService rapports)
    {
        _rapports = rapports;
        _corrections = corrections;
        _correctionsDeStats = correctionsDeStats;
        _gameService = gameService;
        _seriesService = seriesService;
    }

    /// <summary>Saisit l'Équipe gagnante d'une game. Permis à tout utilisateur connecté.</summary>
    [HttpPut("{id}/result")]
    [ProducesResponseType<SetGameResultResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SetGameResultResponse>> SetGameResult(int id, [FromBody] SetGameResultRequest request)
    {
        var result = await _seriesService.RecordResultAsync(id, request.Winner);
        if (!result.IsOk) return this.Refusal(result);

        var game = result.Value!;
        return new SetGameResultResponse(
            GameId: game.Id,
            Winner: game.WinningTeam,
            TeamASide: game.TeamASide,
            TeamBSide: game.TeamBSide,
            Status: game.Status
        );
    }

    /// <summary>
    /// Corrige le vainqueur et/ou la composition d'une game terminée (Administrateur). La Série et le
    /// cost du mois sont recalculés ; la game est marquée « modifiée par un admin ».
    /// </summary>
    [HttpPut("{id:int}/correction")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<GameRecapResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<GameRecapResponse>> CorrigerGame(int id, [FromBody] CorrectGameRequest request)
    {
        var result = await _corrections.CorrigerAsync(id, request.Winner, request.TeamA, request.TeamB);
        if (!result.IsOk) return this.Refusal(result);
        return (await _gameService.GetRecapAsync(id))!;
    }

    /// <summary>
    /// Import d'un Rapport (Administrateur) : dépose sur une game terminée le Fichier de Rapport, le corps
    /// exact d'un rapport du Companion (<c>championId</c> numérique compris). Même validation que le rapport ;
    /// ses participants, ses Sides et son vainqueur doivent être ceux de la game, sinon le refus est complet
    /// (409) et liste les écarts. L'import ne change jamais le vainqueur ni la composition : il donne à la
    /// game ses données Riot, champions et stats, et recalcule les costs. Un id de champion inconnu du
    /// catalogue laisse la game inchangée (503).
    /// </summary>
    [HttpPost("{id:int}/import-rapport")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<GameRecapResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ImportDeRapportRefuseResponse>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<GameRecapResponse>> ImporterUnRapport(int id, [FromBody] RapportDeGameRequest request)
    {
        var result = await _rapports.ImporterAsync(id, request.ToRapport());
        if (!result.IsOk) return this.Refusal(result);
        if (!result.Value!.EstAccepte)
            return new ObjectResult(new ImportDeRapportRefuseResponse(
                "Import refusé", StatusCodes.Status409Conflict,
                "Le fichier ne correspond pas à la game : rien n'a été importé.", result.Value.Ecarts.ToList()))
            {
                StatusCode = StatusCodes.Status409Conflict,
                ContentTypes = { "application/problem+json" }
            };
        return (await _gameService.GetRecapAsync(id))!;
    }

    /// <summary>
    /// Corrige les stats d'un joueur dans une game terminée (Administrateur) : champion, voie, K/D/A, CS,
    /// or, dégâts, vision, note. Les corrections sont stockées à part, les données du Companion restent
    /// intactes ; la game est marquée « modifiée par un admin » et le cost du joueur est recalculé.
    /// </summary>
    [HttpPut("{id:int}/players/{playerId:int}/corrections")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<GameRecapResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<GameRecapResponse>> CorrigerStats(int id, int playerId, [FromBody] CorrectPlayerStatsRequest request)
    {
        var result = await _correctionsDeStats.CorrigerAsync(id, playerId, request.ToDemande());
        if (!result.IsOk) return this.Refusal(result);
        return (await _gameService.GetRecapAsync(id))!;
    }

    /// <summary>
    /// Annule la correction d'un champ (« champion », « position », « kills », « deaths », « assists »,
    /// « cs », « goldEarned », « damageToChampions », « visionScore », « note ») : la valeur d'origine
    /// du Companion est rétablie (Administrateur).
    /// </summary>
    [HttpDelete("{id:int}/players/{playerId:int}/corrections/{champ}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<GameRecapResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<GameRecapResponse>> AnnulerCorrectionDeStat(int id, int playerId, string champ)
    {
        var result = await _correctionsDeStats.AnnulerAsync(id, playerId, champ);
        if (!result.IsOk) return this.Refusal(result);
        return (await _gameService.GetRecapAsync(id))!;
    }

    /// <summary>
    /// Supprime une game terminée (Administrateur). La Série et le cost du mois sont recalculés ; une
    /// Série sans autre game terminée est effacée.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> SupprimerGame(int id)
    {
        var result = await _corrections.SupprimerAsync(id);
        if (!result.IsOk) return this.Refusal(result);
        return NoContent();
    }

    /// <summary>
    /// La page d'une game, en attente ou terminée : sa place dans la Série, ses Équipes, son vainqueur
    /// et les stats du Companion. C'est le lien de récap posté par le bot.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<GameRecapResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<GameRecapResponse>> GetGame(int id) =>
        await _gameService.GetRecapAsync(id) is { } recap
            ? recap
            : Problem(statusCode: StatusCodes.Status404NotFound, detail: "Cette game n'existe pas.");

    [HttpGet("pending")]
    [ProducesResponseType<List<PendingGameResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingGames()
    {
        var pendingGames = await _gameService.GetPendingGamesAsync();

        var response = pendingGames.Select(g => new PendingGameResponse(
            Id: g.Id,
            PlayedAt: g.PlayedAt,
            SeriesId: g.SeriesId,
            Status: g.Status,
            Participants: g.Participants.Select(p => new PendingGameParticipant(
                PlayerId: p.PlayerId,
                Side: p.Side,
                Equipe: g.EquipeOn(p.Side),
                Prenom: p.Player.Prenom
            )).ToList()
        )).ToList();

        return Ok(response);
    }

    [HttpGet("history/{year}/{month}")]
    [ProducesResponseType<GameHistoryListResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(int year, int month)
    {
        var games = await _gameService.GetHistoryAsync(year, month);
        return Ok(new GameHistoryListResponse(games));
    }
}
