using Shameless.Application.Stats;

namespace Shameless.Application.DTOs.Match;

public record GameHistoryResponse(
    int Id,
    int? SeriesId,
    DateTime PlayedAt,
    string TeamASide,
    string TeamBSide,
    string? Winner, // Équipe gagnante : "A" | "B"
    string? RiotMatchId, // null sans rapport du Companion (saisie manuelle)
    DonneesDuMatch? Match, // null sans données Riot (saisie manuelle)
    List<GameHistoryParticipantDto> TeamA,
    List<GameHistoryParticipantDto> TeamB
);

/// <summary>Un participant d'une game de l'historique, avec son champion joué et ses stats envoyées par le Companion.</summary>
/// <param name="ChampionId">Le champion joué (id Data Dragon), null sans rapport du Companion pour ce participant.</param>
/// <param name="Stats">Ses stats pour la game, null sans rapport du Companion pour ce participant.</param>
public record GameHistoryParticipantDto(
    int PlayerId,
    string Prenom,
    string RiotId,
    string? RankTier,
    int? RankDivision,
    decimal? Cost,
    string? ChampionId,
    StatsDuParticipant? Stats
)
{
    public static GameHistoryParticipantDto From(Entities.CustomGameParticipant participant) =>
        new(participant.PlayerId, participant.Player.Prenom, participant.Player.RiotId, participant.Player.RankTier,
            participant.Player.RankDivision, participant.Player.CurrentCost, participant.ChampionId,
            StatsDuParticipant.Lire(participant.RiotParticipantData));
}

public record GameParticipantDto(
    string Prenom,
    string RiotId,
    string? RankTier,
    int? RankDivision,
    decimal? Cost,
    int PlayerId,
    Picker.Role? Role = null // le rôle attribué par l'Autorole, quand il y en a un
)
{
    public static GameParticipantDto From(Entities.Player player, Picker.Role? role = null) =>
        new(player.Prenom, player.RiotId, player.RankTier, player.RankDivision, player.CurrentCost, player.Id, role);
}
