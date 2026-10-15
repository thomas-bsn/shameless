namespace Shameless.Infrastructure.Options;

/// <summary>Réglages de session et d'autorisation, section « Auth ».</summary>
public sealed class AuthOptions
{
    /// <summary>Origines autorisées comme URL de retour après login, séparées par des virgules.</summary>
    public string AllowedReturnOrigins { get; init; } = "";

    /// <summary>IDs Discord des admins, séparés par des virgules.</summary>
    public string AdminDiscordIds { get; init; } = "";

    /// <summary>Durée de la session, prolongée à chaque utilisation.</summary>
    public int SessionLifetimeDays { get; init; } = 14;

    public IReadOnlyList<string> ReturnOrigins => Split(AllowedReturnOrigins);

    public IReadOnlyList<string> AdminIds => Split(AdminDiscordIds);

    private static string[] Split(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
