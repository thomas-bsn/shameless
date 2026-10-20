using Shameless.Application.Stats;

namespace Shameless.Application.DTOs.Match;

/// <summary>Une game terminée de l'historique.</summary>
/// <param name="ModifieeParAdminLe">Quand un Administrateur a corrigé la game ; null si elle est telle que saisie.</param>
/// <param name="Dofd">Les Joueurs élus DOFD par les votes, tous en cas d'égalité ; vide tant que les votes ne sont pas clos.</param>
public record GameHistoryResponse(
    int Id,
    int? SeriesId,
    DateTime PlayedAt,
    DateTime? ModifieeParAdminLe,
    string TeamASide,
    string TeamBSide,
    string? Winner, // Équipe gagnante : "A" | "B"
    string? RiotMatchId, // null sans rapport du Companion (saisie manuelle)
    DonneesDuMatch? Match, // null sans données Riot (saisie manuelle)
    List<GameHistoryParticipantDto> TeamA,
    List<GameHistoryParticipantDto> TeamB,
    List<int> Dofd
);

/// <summary>Un participant d'une game de l'historique, avec son champion joué et ses stats envoyées par le Companion.</summary>
/// <param name="ChampionId">Le champion joué (id Data Dragon), null sans rapport du Companion pour ce participant.</param>
/// <param name="Stats">Ses stats pour la game, null sans rapport du Companion pour ce participant.</param>
/// <param name="Performance">Sa note de performance sur 100 et ses composantes, null sans stats suffisantes.</param>
/// <param name="Details">Son rôle joué et sa source, ses objets, son objet de rôle, ses sorts, ses runes et ses valeurs dérivées (par minute, parts de l'Équipe), sur les valeurs effectives.</param>
/// <param name="Place">Sa place dans la game, de 1 à 10, d'après sa note de performance ; null sans note.</param>
/// <param name="Badge">Le badge « Meilleure perf » (meilleure note de l'Équipe gagnante) ou « ACE » (meilleure note de l'Équipe perdante) ; null sinon.</param>
/// <param name="Impact">Ses sous-scores Global / Vs adversaire / Objectifs / Équipe / Rôle (lettre et lignes tirées de la note) et son face-à-face à six axes contre l'adversaire du même rôle ; null sans stats. Une stat absente masque sa ligne ou son axe.</param>
/// <param name="ChampsCorriges">Les champs (champion, position, kills, deaths, assists, cs, goldEarned, damageToChampions, visionScore, note) dont la valeur affichée est corrigée par un admin ; vide si aucun.</param>
public record GameHistoryParticipantDto(
    int PlayerId,
    string Prenom,
    string RiotId,
    string? RankTier,
    int? RankDivision,
    decimal? Cost,
    string? ChampionId,
    StatsDuParticipant? Stats,
    NoteDePerformance? Performance,
    List<string> ChampsCorriges,
    string? AvatarUrl = null, // la photo Discord du compte lié au Joueur
    StatsDuParticipant? StatsOrigine = null, // ses stats telles que le Companion les a envoyées, sans correction
    string? ChampionOrigine = null, // le champion du rapport du Companion, sans correction
    DetailsDuJoueur? Details = null, // rôle joué, objets, sorts, runes, valeurs par minute et parts d'équipe
    int? Place = null, // sa place de 1 à 10 d'après sa note ; null sans note
    BadgeDeGame? Badge = null, // « Meilleure perf » ou « ACE » ; null sans badge
    ImpactDuJoueur? Impact = null // sous-scores par groupe (lettre, lignes de la note) et face-à-face à six axes ; null sans stats
)
{
    public static GameHistoryParticipantDto From(Entities.CustomGameParticipant participant, LectureDuJoueur lecture) =>
        new(participant.PlayerId, participant.Player.Prenom, participant.Player.RiotId, participant.Player.RankTier,
            participant.Player.RankDivision, participant.Player.CurrentCost, ValeursEffectives.Champion(participant),
            ValeursEffectives.Stats(participant), lecture.Note,
            participant.Corrections.Select(c => Entities.ChampCorrigeableExtensions.Nom(c.Champ)).Order().ToList(),
            participant.Player.User?.AvatarUrl, ValeursEffectives.StatsOrigine(participant), ValeursEffectives.ChampionOrigine(participant),
            lecture.Details, lecture.Place, lecture.Badge, lecture.Impact);
}

public record GameParticipantDto(
    string Prenom,
    string RiotId,
    string? RankTier,
    int? RankDivision,
    decimal? Cost,
    int PlayerId,
    Picker.Role? Role = null, // le rôle attribué par l'Autorole, quand il y en a un
    string? AvatarUrl = null // la photo Discord du compte lié au Joueur
)
{
    public static GameParticipantDto From(Entities.Player player, Picker.Role? role = null) =>
        new(player.Prenom, player.RiotId, player.RankTier, player.RankDivision, player.CurrentCost, player.Id, role, player.User?.AvatarUrl);
}
