namespace Shameless.Api.Models.Requests.Series;

/// <summary>Une Série entre l'Équipe A et l'Équipe B (cinq joueurs chacune), au format 1, 2, 3 ou 5.</summary>
public sealed record CreateSeriesRequest(
    List<int> TeamA,
    List<int> TeamB,
    int Format,
    bool Fearless = false,
    bool AlternateSides = false
);

/// <summary>
/// La prochaine game : Side de l'Équipe A ("blue" | "red"). Facultatif avec l'alternance
/// automatique, qui inverse alors le Side de la game précédente.
/// </summary>
public sealed record PlayGameRequest(string? TeamASide);

/// <summary>
/// Revanche : les mêmes Équipes rejouent une nouvelle Série, au format 1, 2, 3 ou 5 choisi ici,
/// avec ou sans Fearless et alternance automatique des Sides.
/// </summary>
public sealed record RematchRequest(
    int Format,
    bool Fearless = false,
    bool AlternateSides = false
);

/// <summary>Remplacement : le joueur remplacé quitte son Équipe, le remplaçant prend sa place pour la prochaine game.</summary>
public sealed record ReplacePlayerRequest(int ReplacedPlayerId, int ReplacementPlayerId);

/// <summary>
/// Désigne le Capitaine d'une Équipe : "Manuel" (avec <c>PlayerId</c>, un joueur de l'Équipe),
/// "AuHasard" ou "PlusGrosCost" (le plus gros cost de l'Équipe).
/// </summary>
public sealed record DesignerCapitaineRequest(Application.Entities.DesignationCapitaine Designation, int? PlayerId = null);
