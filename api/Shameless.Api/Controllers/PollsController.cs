using System.Security.Claims;
using Shameless.Api.Contracts;
using Shameless.Api.Models.Requests.Poll;
using Shameless.Application.DTOs.Poll;
using Shameless.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Shameless.Api.Controllers;

[ApiController]
[Route("polls")]
public sealed class PollsController : ControllerBase
{
    private readonly PollStore _polls;

    public PollsController(PollStore polls)
    {
        _polls = polls;
    }

    [HttpPost]
#if DEBUG
    [AllowAnonymous]
#else
    [Authorize(Roles = "Admin")]
#endif
    public ActionResult<PollDto> OpenPoll([FromBody] OpenPollRequest req)
    {
        var poll = _polls.Open(req.DurationHours);
        return Ok(poll);
    }

    [HttpGet("current")]
    public ActionResult<PollDto> GetCurrentPoll()
    {
        var poll = _polls.GetCurrent();
        if (poll is null) return NotFound(new { error = "no_poll" });
        return Ok(poll);
    }

    [HttpPut("current/ballot")]
    [Authorize]
    public IActionResult SubmitBallot([FromBody] SubmitBallotRequest req)
    {
        var voterId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(voterId))
            return Unauthorized();

        var dto = new SubmitBallotDto(
            req.Votes.Select(v => new VoteDto(v.PlayerId, (Grade)v.Grade)).ToList()
        );

        _polls.UpsertBallot(voterId, dto);
        return Ok(new { ok = true });
    }

    [HttpGet("current/results")]
    public ActionResult<PollResultsDto> GetResults()
    {
        return Ok(_polls.GetResults());
    }
}