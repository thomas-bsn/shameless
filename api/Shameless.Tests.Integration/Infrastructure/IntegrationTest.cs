using System.Net.Http.Json;
using System.Text.Json;

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>Chaque test part d'une base vide.</summary>
public abstract class IntegrationTest(ApiFactory factory) : IAsyncLifetime
{
    protected ApiFactory Factory { get; } = factory;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await Factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected async Task<int> CreatePlayerAsync(HttpClient client, string prenom, string tier = "GOLD", string division = "II")
    {
        var riotId = $"{prenom}#EUW";
        Factory.Riot.AddAccount(riotId, tier, division);
        var response = await client.PostAsJsonAsync("/players", new { prenom, riotId }, Ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.GetProperty("id").GetInt32();
    }

    protected async Task<int[]> CreatePlayersAsync(HttpClient client, params string[] prenoms)
    {
        var ids = new List<int>();
        foreach (var prenom in prenoms)
            ids.Add(await CreatePlayerAsync(client, prenom));
        return ids.ToArray();
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
}
