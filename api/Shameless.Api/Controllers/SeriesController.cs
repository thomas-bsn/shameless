using Shameless.Api.Auth;
using Shameless.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Shameless.Api.Controllers;

[ApiController]
[Route("series")]
public sealed class SeriesController : ControllerBase
{
    private readonly CustomGameService _gameService;

    public SeriesController(CustomGameService gameService)
    {
        _gameService = gameService;
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActiveSeries()
    {
        var series = await _gameService.GetActiveSeriesAsync();
        return Ok(new { series });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSeriesStatus(int id)
    {
        var status = await _gameService.GetSeriesStatusAsync(id);
        if (status == null) return NotFound();
        return Ok(status);
    }

    [HttpPut("{id}/abandon")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> AbandonSeries(int id)
    {
        var ok = await _gameService.AbandonSeriesAsync(id);
        if (!ok) return NotFound();
        return Ok(new { success = true });
    }
}