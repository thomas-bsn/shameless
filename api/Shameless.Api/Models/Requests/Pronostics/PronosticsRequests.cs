namespace Shameless.Api.Models.Requests.Pronostics;

/// <summary>Le pronostic d'un Spectateur sur la prochaine Game d'une Série, déposé par le bot.</summary>
/// <param name="PronostiqueurDiscordId">L'ID Discord (texte) du Spectateur.</param>
/// <param name="PronostiqueurNom">Son nom affiché sur Discord, pour le récap ; facultatif.</param>
/// <param name="GameNumber">Le numéro de la Game pronostiquée : la prochaine de la Série.</param>
/// <param name="EquipeGagnante">« A » ou « B », null sans choix.</param>
/// <param name="MvpPlayerId">Le Joueur pronostiqué MVP, null sans choix.</param>
/// <param name="DofdPlayerId">Le Joueur pronostiqué DOFD, null sans choix.</param>
public sealed record PronostiquerRequest(
    string PronostiqueurDiscordId,
    string? PronostiqueurNom,
    int GameNumber,
    string? EquipeGagnante,
    int? MvpPlayerId,
    int? DofdPlayerId);
