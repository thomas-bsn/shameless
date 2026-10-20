namespace Shameless.Api.Models.Requests.Votes;

/// <summary>Le bulletin d'un votant, déposé par le bot.</summary>
/// <param name="VotantDiscordId">L'ID Discord (texte) du Joueur ou du Spectateur qui vote.</param>
/// <param name="DofdPlayerId">Le Joueur choisi comme DOFD, null pour s'abstenir.</param>
public sealed record VoterRequest(string VotantDiscordId, int? DofdPlayerId);

/// <summary>Les Spectateurs présents en vocal pendant la Game, par ID Discord (texte) : remplace la liste.</summary>
public sealed record SignalerSpectateursRequest(List<string> DiscordIds);
