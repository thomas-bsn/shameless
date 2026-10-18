using Microsoft.EntityFrameworkCore;
using Shameless.Api.Extensions;
using Shameless.Application.DTOs.Users;
using Shameless.Application.Interfaces;
using Shameless.Application.Services;
using Shameless.Infrastructure.Data.Repositories;

namespace Shameless.Api.Cli;

/// <summary>
/// La commande de l'image de l'API (ADR 0017), seul moyen avec le Wizard de créer ou retirer un
/// Superadministrateur : <c>superadmin ajouter | retirer | lister</c>. Elle parle à la base sans démarrer le site.
/// Codes de sortie : 0 fait, 1 refusé (dernier Superadministrateur, pas Superadministrateur), 2 usage.
/// </summary>
public static class SuperadminCli
{
    public const string Usage = """
        Usage :
          superadmin ajouter <identifiant Discord>   donne les droits de Superadministrateur
          superadmin retirer <identifiant Discord>   les retire (refusé pour le dernier)
          superadmin lister                          liste les Superadministrateurs
        """;

    /// <summary>Vrai si les arguments demandent cette commande (le premier mot est « superadmin »).</summary>
    public static bool Concerne(string[] args) => args.Length > 0 && args[0] == "superadmin";

    /// <param name="args">Les arguments complets, « superadmin » compris.</param>
    public static async Task<int> RunAsync(string[] args, IConfiguration configuration, TextWriter output, TextWriter error)
    {
        var action = args.Length > 1 ? args[1] : null;
        long id = 0;
        var valide = action switch
        {
            "lister" => args.Length == 2,
            "ajouter" or "retirer" => args.Length == 3 && long.TryParse(args[2], out id) && id > 0,
            _ => false
        };
        if (!valide)
        {
            await error.WriteLineAsync(Usage);
            return 2;
        }

        var connection = configuration.GetConnectionString("Shameless");
        if (string.IsNullOrWhiteSpace(connection))
        {
            await error.WriteLineAsync("Connexion à la base introuvable (ConnectionStrings__Shameless, ou /instance/instance.env).");
            return 2;
        }

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<RolesService>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RolesService>();

        try
        {
            return action switch
            {
                "ajouter" => await Ajouter(roles, id, output),
                "retirer" => await Retirer(roles, id, output, error),
                _ => await Lister(roles, FuseauDe(configuration), output)
            };
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException or DbUpdateException)
        {
            await error.WriteLineAsync($"Base de données inaccessible ou pas migrée : {ex.GetBaseException().Message}");
            return 1;
        }
    }

    private static async Task<int> Ajouter(RolesService roles, long id, TextWriter output)
    {
        await roles.AddSuperadminAsync(id);
        await output.WriteLineAsync($"{id} est Superadministrateur.");
        return 0;
    }

    private static async Task<int> Retirer(RolesService roles, long id, TextWriter output, TextWriter error)
    {
        switch (await roles.RemoveSuperadminAsync(id))
        {
            case RoleResult.Ok:
                await output.WriteLineAsync($"{id} n'est plus Superadministrateur.");
                return 0;
            case RoleResult.LastSuperadmin:
                await error.WriteLineAsync($"Refusé : {id} est le dernier Superadministrateur. Ajoutez-en un autre avant de le retirer.");
                return 1;
            default:
                await error.WriteLineAsync($"Refusé : {id} n'est pas Superadministrateur.");
                return 1;
        }
    }

    private static async Task<int> Lister(RolesService roles, TimeZoneInfo fuseau, TextWriter output)
    {
        var superadmins = await roles.ListSuperadminsAsync();
        if (superadmins.Count == 0)
            await output.WriteLineAsync("Aucun Superadministrateur.");
        foreach (var u in superadmins)
            await output.WriteLineAsync(Ligne(u, fuseau));
        return 0;
    }

    /// <summary>« 123  Pseudo  promu par X, le JJ/MM/AAAA ».</summary>
    public static string Ligne(UserDto u, TimeZoneInfo fuseau)
    {
        var date = u.PromotedAt is { } at
            ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(at, DateTimeKind.Utc), fuseau).ToString("dd/MM/yyyy")
            : null;
        var promotion = u.PromotedBy is null ? "" : $"promu par {u.PromotedBy}, le {date}";
        return $"{u.DiscordId}  {u.DiscordUsername ?? "(jamais connecté)"}  {promotion}".TrimEnd();
    }

    private static TimeZoneInfo FuseauDe(IConfiguration configuration)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(configuration["Instance:TimeZone"] ?? "Europe/Paris"); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }
}
