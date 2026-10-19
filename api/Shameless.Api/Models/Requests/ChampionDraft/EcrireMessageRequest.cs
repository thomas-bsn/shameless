namespace Shameless.Api.Models.Requests.ChampionDraft;

/// <summary>
/// Un message du chat de la draft. <paramref name="SpectateurId"/> : jeton aléatoire que garde le navigateur
/// d'un spectateur sans connexion, pour garder son pseudo automatique (« Spectateur 1 ») et son débit.
/// Ignoré pour un Capitaine et pour un spectateur connecté avec Discord.
/// </summary>
public sealed record EcrireMessageRequest(string? Contenu, string? SpectateurId);
