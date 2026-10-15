using Microsoft.AspNetCore.Hosting;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Le document OpenAPI commité dans contracts/ est la source du client TypeScript du front.
/// Pour le régénérer : UPDATE_CONTRACTS=1 dotnet test --filter-class *ContractTests
/// </summary>
public class ContractTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task The_committed_openapi_contract_matches_the_api()
    {
        await using var development = Factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));
        var actual = await development.CreateClient().GetStringAsync("/openapi/v1.json", Ct);
        var path = Path.Combine(RepositoryRoot(), "contracts", "openapi.json");

        if (Environment.GetEnvironmentVariable("UPDATE_CONTRACTS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, actual + "\n", Ct);
            return;
        }

        Assert.True(File.Exists(path), $"{path} est absent : lancer les tests avec UPDATE_CONTRACTS=1.");
        var committed = await File.ReadAllTextAsync(path, Ct);
        Assert.True(
            Normalize(committed) == Normalize(actual),
            "contracts/openapi.json n'est plus à jour : lancer les tests avec UPDATE_CONTRACTS=1, puis régénérer le client du front.");
    }

    private static string Normalize(string json) => json.ReplaceLineEndings("\n").Trim();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "compose.yaml")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Racine du dépôt introuvable (compose.yaml).");
    }
}
