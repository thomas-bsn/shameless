using Microsoft.Extensions.Options;
using Shameless.Application.Services;
using Shameless.Infrastructure.Options;

namespace Shameless.Api.Auth;

/// <summary>
/// Au premier démarrage après l'arrivée des rôles en base, les identifiants de <c>Auth:AdminDiscordIds</c>
/// deviennent Superadministrateurs (ADR 0017). Dès qu'un Superadministrateur existe, la configuration n'est
/// plus lue pour les droits : relancer l'API ne change rien.
/// </summary>
public sealed class ImportDesSuperadministrateursDeLaConfiguration(
    IServiceScopeFactory scopes,
    IOptions<AuthOptions> options,
    ILogger<ImportDesSuperadministrateursDeLaConfiguration> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var ids = new List<long>();
        foreach (var raw in options.Value.AdminIds)
        {
            if (long.TryParse(raw, out var id)) ids.Add(id);
            else logger.LogWarning("Auth:AdminDiscordIds : « {Id} » n'est pas un identifiant Discord, ignoré.", raw);
        }

        await using var scope = scopes.CreateAsyncScope();
        var imported = await scope.ServiceProvider.GetRequiredService<RolesService>().ImportConfiguredSuperadminsAsync(ids);
        if (imported > 0)
            logger.LogInformation("{Count} Superadministrateur(s) importé(s) depuis Auth:AdminDiscordIds.", imported);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
