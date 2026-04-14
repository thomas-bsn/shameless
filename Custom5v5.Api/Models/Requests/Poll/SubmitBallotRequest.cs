namespace Custom5v5.Api.Models.Requests.Poll;

public sealed record SubmitBallotRequest(List<VoteRequest> Votes);

public sealed record VoteRequest(int PlayerId, int Grade);