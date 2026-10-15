namespace Custom5v5.Application.DTOs.Riot;

public sealed record MatchResponse(MatchInfo? Info);
public sealed record MatchInfo(List<MatchParticipant>? Participants);
public sealed record MatchParticipant(string Puuid, string TeamPosition);