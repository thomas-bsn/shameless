using System.Collections.Concurrent;
using Custom5v5.Api.Interfaces;
using Custom5v5.Application.DTOs.Poll;
using Custom5v5.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Custom5v5.Infrastructure.Services;

public sealed class PollStore
{
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly object _lock = new();

    private PollState? _current;
    private readonly ConcurrentDictionary<string, Ballot> _ballotsByVoter = new();

    public PollStore(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public PollDto Open(int durationHours)
    {
        durationHours = Math.Clamp(durationHours, 1, 72);

        lock (_lock)
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var now = DateTime.UtcNow;
            var playersSnapshot = repo.GetAllAsync().Result
                .Select(p => new PollPlayerDto(p.Id, p.Prenom))
                .ToList();

            if (playersSnapshot.Count == 0)
                throw new InvalidOperationException("No players available to vote on.");

            _current = new PollState(
                pollId: Guid.NewGuid(),
                opensAtUtc: now,
                closesAtUtc: now.AddHours(durationHours),
                playersSnapshot: playersSnapshot
            );

            _ballotsByVoter.Clear();
            return ToDto(_current);
        }
    }

    public PollDto? GetCurrent()
    {
        lock (_lock)
        {
            if (_current is null) return null;
            CloseIfExpired_NoThrow(_current);
            return ToDto(_current);
        }
    }

    public void UpsertBallot(string voterId, SubmitBallotDto req)  // ← SubmitBallotDto
    {
        if (string.IsNullOrWhiteSpace(voterId))
            throw new ArgumentException("voterId is required.", nameof(voterId));

        var poll = GetCurrentInternalOrThrow();

        if (poll.Status != PollStatus.Open)
            throw new InvalidOperationException("Poll is closed.");

        var map = new Dictionary<int, Grade>();
        foreach (var v in req.Votes ?? Array.Empty<VoteDto>())
        {
            if (!poll.PlayersSnapshotById.ContainsKey(v.PlayerId))
                continue;

            map[v.PlayerId] = v.Grade;
        }

        _ballotsByVoter[voterId] = new Ballot(map, DateTime.UtcNow);
    }

    public PollResultsDto GetResults()
    {
        var poll = GetCurrentInternalOrThrow();
        var players = poll.PlayersSnapshot;

        var acc = players.ToDictionary(p => p.Id, p => new Counts(p.DisplayName));

        foreach (var ballot in _ballotsByVoter.Values)
        {
            foreach (var player in players)
            {
                var grade = ballot.Votes.TryGetValue(player.Id, out var g) ? g : Grade.Blank;
                acc[player.Id].Add(grade);
            }
        }

        var results = acc.Select(kvp =>
        {
            var counts = kvp.Value;
            var total = counts.Total;
            Grade? majority = total == 0 ? null : counts.Majority;

            return new PlayerResultDto(
                kvp.Key,
                counts.DisplayName,
                counts.A, counts.B, counts.C, counts.D, counts.Blank,
                total,
                majority
            );
        }).ToList();

        return new PollResultsDto(poll.PollId, results);
    }

    public (bool HasBallot, SubmitBallotDto? Ballot) TryGetMyBallot(string voterId)  // ← SubmitBallotDto
    {
        if (string.IsNullOrWhiteSpace(voterId))
            return (false, null);

        var poll = GetCurrentInternalOrThrow();

        if (!_ballotsByVoter.TryGetValue(voterId, out var ballot))
            return (false, null);

        var votes = ballot.Votes
            .Select(kv => new VoteDto(kv.Key, kv.Value))
            .ToList();

        return (true, new SubmitBallotDto(votes));
    }

    private PollState GetCurrentInternalOrThrow()
    {
        lock (_lock)
        {
            if (_current is null)
                throw new InvalidOperationException("No poll open.");

            CloseIfExpired_NoThrow(_current);
            return _current;
        }
    }

    private static void CloseIfExpired_NoThrow(PollState poll)
    {
        if (poll.Status == PollStatus.Closed) return;
        if (DateTime.UtcNow >= poll.ClosesAtUtc)
            poll.Status = PollStatus.Closed;
    }

    private static PollDto ToDto(PollState poll)
        => new(
            poll.PollId,
            poll.OpensAtUtc,
            poll.ClosesAtUtc,
            poll.Status.ToString(),
            poll.PlayersSnapshot
        );

    private sealed class PollState
    {
        public PollState(Guid pollId, DateTime opensAtUtc, DateTime closesAtUtc, IReadOnlyList<PollPlayerDto> playersSnapshot)  // ← PollPlayerDto
        {
            PollId = pollId;
            OpensAtUtc = opensAtUtc;
            ClosesAtUtc = closesAtUtc;
            PlayersSnapshot = playersSnapshot;
            PlayersSnapshotById = playersSnapshot.ToDictionary(p => p.Id, p => p);
        }

        public Guid PollId { get; }
        public DateTime OpensAtUtc { get; }
        public DateTime ClosesAtUtc { get; }
        public PollStatus Status { get; set; } = PollStatus.Open;
        public IReadOnlyList<PollPlayerDto> PlayersSnapshot { get; }  // ← PollPlayerDto
        public IReadOnlyDictionary<int, PollPlayerDto> PlayersSnapshotById { get; }  // ← PollPlayerDto
    }

    private enum PollStatus { Open = 0, Closed = 1 }

    private sealed record Ballot(Dictionary<int, Grade> Votes, DateTime UpdatedAtUtc);

    private sealed class Counts
    {
        public Counts(string displayName) => DisplayName = displayName;

        public string DisplayName { get; }
        public int A { get; private set; }
        public int B { get; private set; }
        public int C { get; private set; }
        public int D { get; private set; }
        public int Blank { get; private set; }
        public int Total => A + B + C + D + Blank;

        public void Add(Grade g)
        {
            switch (g)
            {
                case Grade.A: A++; break;
                case Grade.B: B++; break;
                case Grade.C: C++; break;
                case Grade.D: D++; break;
                default: Blank++; break;
            }
        }

        public Grade Majority
        {
            get
            {
                var dict = new Dictionary<Grade, int>
                {
                    { Grade.A, A }, { Grade.B, B }, { Grade.C, C },
                    { Grade.D, D }, { Grade.Blank, Blank }
                };

                return dict
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key == Grade.Blank ? 1 : 0)
                    .ThenBy(kv => kv.Key)
                    .First().Key;
            }
        }
    }
}