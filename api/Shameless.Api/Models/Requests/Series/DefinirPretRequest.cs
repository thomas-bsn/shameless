namespace Shameless.Api.Models.Requests.Series;

/// <summary>Le Capitaine se met « prêt » (<c>true</c>) ou se remet « pas prêt » (<c>false</c>).</summary>
public sealed record DefinirPretRequest(bool Pret);
