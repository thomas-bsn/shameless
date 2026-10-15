// Application/DTOs/Poll/PollDtos.cs
namespace Custom5v5.Application.DTOs.Poll;

public sealed record PollPlayerDto(int Id, string DisplayName);  // ← int

public enum Grade { A, B, C, D, Blank }

public sealed record OpenPollDto(int DurationHours = 24);

public sealed record PollDto(
    Guid PollId,
    DateTime OpensAtUtc,
    DateTime ClosesAtUtc,
    string Status,
    IReadOnlyList<PollPlayerDto> Players
);

public sealed record SubmitBallotDto(IReadOnlyList<VoteDto> Votes);

public sealed record VoteDto(int PlayerId, Grade Grade);  // ← int

public sealed record PollResultsDto(Guid PollId, IReadOnlyList<PlayerResultDto> Results);

public sealed record PlayerResultDto(
    int PlayerId,  // ← int
    string DisplayName,
    int A, int B, int C, int D, int Blank,
    int Total,
    Grade? Majority
);