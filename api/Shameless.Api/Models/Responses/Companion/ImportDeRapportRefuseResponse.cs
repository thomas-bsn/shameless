namespace Shameless.Api.Models.Responses.Companion;

/// <summary>
/// Un Import d'un Rapport refusé en entier (409, application/problem+json) : le fichier ne correspond pas
/// à la game. <c>Ecarts</c> liste chaque différence (participants, Sides, vainqueur) ; la game n'a pas changé.
/// </summary>
public sealed record ImportDeRapportRefuseResponse(string Title, int Status, string Detail, List<string> Ecarts);
