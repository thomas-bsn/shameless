using System.Net;
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

    /// <summary>L'Utilisateur Discord <paramref name="discordId"/> se connecte et se lie à ce joueur.</summary>
    protected async Task LierADiscordAsync(int playerId, string discordId) =>
        await ReadJsonAsync(await Factory.CreateSignedInClient(discordId)
            .PostAsJsonAsync("/players/link", new { playerId }, Ct));

    /// <summary>Ouvre une Soirée et renvoie son id.</summary>
    protected async Task<int> OpenSoireeAsync(HttpClient client)
    {
        var soiree = await ReadJsonAsync(await client.PostAsync("/soirees", null, Ct));
        return soiree.GetProperty("id").GetInt32();
    }

    /// <summary>
    /// Ouvre une Soirée si besoin, enregistre dix joueurs (A1…A5, B1…B5) et crée une Série entre
    /// l'Équipe A (A1…A5) et l'Équipe B (B1…B5).
    /// </summary>
    protected async Task<CreatedSerie> CreateSerieAsync(HttpClient client, int format = 3, bool fearless = false, bool alternateSides = false)
    {
        var open = await ReadJsonAsync(await client.GetAsync("/soirees/open", Ct));
        if (open.GetProperty("soiree").ValueKind == JsonValueKind.Null)
            await OpenSoireeAsync(client);

        var ids = await CreatePlayersAsync(client, "A1", "A2", "A3", "A4", "A5", "B1", "B2", "B3", "B4", "B5");
        int[] teamA = ids[..5], teamB = ids[5..];
        var serie = await ReadJsonAsync(await client.PostAsJsonAsync("/series",
            new { teamA, teamB, format, fearless, alternateSides }, Ct));
        return new CreatedSerie(serie.GetProperty("seriesId").GetInt32(), teamA, teamB);
    }

    /// <summary>Lance la prochaine game de la Série et renvoie son id.</summary>
    protected async Task<int> PlayGameAsync(HttpClient client, int seriesId, string? teamASide = "blue")
    {
        await RejoindreLesCapitainesAsync(client, seriesId);
        var game = await ReadJsonAsync(await client.PostAsJsonAsync($"/series/{seriesId}/games", new { teamASide }, Ct));
        return game.GetProperty("id").GetInt32();
    }

    /// <summary>Les deux Capitaines ouvrent leur Lien capitaine : la draft peut être lancée (ADR 0017).</summary>
    protected async Task RejoindreLesCapitainesAsync(HttpClient client, int seriesId)
    {
        var liens = await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}/liens", Ct));
        foreach (var nom in new[] { "lienCapitaineA", "lienCapitaineB" })
            (await Factory.CreateAnonymousClient()
                .GetAsync($"/liens/{Uri.EscapeDataString(liens.GetProperty(nom).GetString()!)}", Ct)).EnsureSuccessStatusCode();
    }

    /// <summary>Lance la prochaine game de la Série et saisit l'Équipe gagnante.</summary>
    protected async Task<int> PlayAndWinAsync(HttpClient client, int seriesId, string winner, string? teamASide = "blue")
    {
        var gameId = await PlayGameAsync(client, seriesId, teamASide);
        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner }, Ct));
        return gameId;
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    /// <summary>Le code Companion de la Série, lu par l'organisateur connecté.</summary>
    protected async Task<string> CodeCompanionAsync(HttpClient client, int seriesId) =>
        (await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}/code-companion", Ct)))
            .GetProperty("code").GetString()!;

    /// <summary>Le Companion, sans connexion, envoie un rapport de fin de game avec ce code.</summary>
    protected async Task<HttpResponseMessage> EnvoyerRapportAsync(string code, object rapport) =>
        await Factory.CreateAnonymousClient().PostAsJsonAsync(
            $"/companion/{Uri.EscapeDataString(code)}/rapports", rapport, Ct);

    /// <summary>Un refus de l'API : ce statut, en problem+json avec un détail lisible.</summary>
    protected static async Task AssertRefusAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var detail = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("detail").GetString();
        Assert.False(string.IsNullOrWhiteSpace(detail));
    }
}

public sealed record CreatedSerie(int SeriesId, int[] TeamA, int[] TeamB);
