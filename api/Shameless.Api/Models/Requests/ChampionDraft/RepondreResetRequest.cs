namespace Shameless.Api.Models.Requests.ChampionDraft;

/// <summary>La réponse à une demande de Reset de la draft : accepter ou refuser.</summary>
public sealed record RepondreResetRequest(bool Accepte);
