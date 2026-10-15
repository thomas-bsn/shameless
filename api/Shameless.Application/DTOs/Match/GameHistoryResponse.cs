namespace Shameless.Application.DTOs.Match;

public record GameHistoryResponse(
    int Id,
    int? SeriesId,
    DateTime PlayedAt,
    string TeamASide,
    string TeamBSide,
    string? Winner, // Équipe gagnante : "A" | "B"
    List<GameParticipantDto> TeamA,
    List<GameParticipantDto> TeamB
);

public record GameParticipantDto(
    string Prenom,
    string RiotId,
    string? RankTier,
    int? RankDivision,
    decimal? Cost,
    int PlayerId
)
{
    public static GameParticipantDto From(Entities.Player player) =>
        new(player.Prenom, player.RiotId, player.RankTier, player.RankDivision, player.CurrentCost, player.Id);
}
