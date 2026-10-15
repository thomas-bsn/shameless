namespace Shameless.Api.Models.Responses.Game;

public sealed record PendingGameResponse(
    int Id,
    DateTime PlayedAt,
    int? SeriesId,
    string Status,
    List<PendingGameParticipant> Participants
);

public sealed record PendingGameParticipant(  
    int PlayerId,
    string Side, // "blue" | "red"
    string Equipe, // "A" | "B"
    string Prenom
);