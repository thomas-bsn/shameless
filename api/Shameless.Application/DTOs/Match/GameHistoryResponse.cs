using Shameless.Application.Stats;

namespace Shameless.Application.DTOs.Match;

/// <summary>Une game terminée de l'historique.</summary>
/// <param name="Mvp">Les Joueurs élus MVP par les votes, tous en cas d'égalité ; vide tant que les votes ne sont pas clos.</param>
/// <param name="Dofd">Les Joueurs élus DOFD par les votes, tous en cas d'égalité ; vide tant que les votes ne sont pas clos.</param>
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
    List<GameHistoryParticipantDto> TeamB,
    List<int> Mvp,
    List<int> Dofd
);

/// <summary>Un participant d'une game de l'historique, avec son champion joué et ses stats envoyées par le Companion.</summary>
/// <param name="ChampionId">Le champion joué (id Data Dragon), null sans rapport du Companion pour ce participant.</param>
/// <param name="Stats">Ses stats pour la game, null sans rapport du Companion pour ce participant.</param>
/// <param name="Performance">Sa note de performance sur 100 et ses composantes, null sans stats suffisantes.</param>
public record GameHistoryParticipantDto(
    int PlayerId,
    string Prenom,
    string RiotId,
    string? RankTier,
    int? RankDivision,
    decimal? Cost,
    string? ChampionId,
    StatsDuParticipant? Stats,
    NoteDePerformance? Performance
)
{
    public static GameHistoryParticipantDto From(Entities.CustomGameParticipant participant, NoteDePerformance? performance) =>
        new(participant.PlayerId, participant.Player.Prenom, participant.Player.RiotId, participant.Player.RankTier,
            participant.Player.RankDivision, participant.Player.CurrentCost, participant.ChampionId,
            StatsDuParticipant.Lire(participant.RiotParticipantData), performance);
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
