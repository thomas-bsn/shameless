using System.Net;
using System.Net.Http.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Des commandes simultanées sur une même Série ou une même session de Picker : une seule réussit,
/// les autres sont refusées en 409.
/// </summary>
public class ConcurrenceTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const int Simultanees = 8;

    private static void UneSeuleReussit(HttpResponseMessage[] responses, HttpStatusCode succes)
    {
        Assert.Single(responses, r => r.StatusCode == succes);
        Assert.All(responses.Where(r => r.StatusCode != succes), r =>
        {
            Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
            Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);
        });
    }

    private async Task<int[]> DixJoueursCochesAsync(HttpClient client)
    {
        await OpenSoireeAsync(client);
        var dix = await CreatePlayersAsync(client, "J1", "J2", "J3", "J4", "J5", "J6", "J7", "J8", "J9", "J10");
        await ReadJsonAsync(await client.PutAsJsonAsync("/soirees/open/picker/selection", new { playerIds = dix }, Ct));
        return dix;
    }

    [Fact]
    public async Task Des_lancements_simultanes_ne_lancent_qu_une_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);

        var responses = await Task.WhenAll(Enumerable.Range(0, Simultanees).Select(_ =>
            client.PostAsJsonAsync($"/series/{serie.SeriesId}/games", new { teamASide = "blue" }, Ct)));

        UneSeuleReussit(responses, HttpStatusCode.Created);
        var series = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        Assert.Single(series.GetProperty("games").EnumerateArray());
    }

    [Fact]
    public async Task Des_saisies_simultanees_ne_saisissent_le_resultat_qu_une_fois()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var gameId = await PlayGameAsync(client, serie.SeriesId);

        var responses = await Task.WhenAll(Enumerable.Range(0, Simultanees).Select(i =>
            client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = i % 2 == 0 ? "A" : "B" }, Ct)));

        UneSeuleReussit(responses, HttpStatusCode.OK);
        var series = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        Assert.Equal(1, series.GetProperty("teamAWins").GetInt32() + series.GetProperty("teamBWins").GetInt32());
    }

    [Fact]
    public async Task Des_picks_simultanes_ne_comptent_qu_un_pick()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DixJoueursCochesAsync(client);
        await ReadJsonAsync(await client.PostAsJsonAsync("/soirees/open/picker/draft",
            new { capitaines = "Manuels", capitaineA = dix[0], capitaineB = dix[1], premierPick = "A" }, Ct));

        var responses = await Task.WhenAll(dix[2..].Select(playerId =>
            client.PostAsJsonAsync("/soirees/open/picker/draft/picks", new { equipe = "A", playerId }, Ct)));

        UneSeuleReussit(responses, HttpStatusCode.OK);
        var open = await ReadJsonAsync(await client.GetAsync("/soirees/open", Ct));
        Assert.Single(open.GetProperty("soiree").GetProperty("picker").GetProperty("picks").EnumerateArray());
    }

    [Fact]
    public async Task Des_validations_simultanees_ne_creent_qu_une_serie()
    {
        var client = Factory.CreateSignedInClient();
        await DixJoueursCochesAsync(client);
        await ReadJsonAsync(await client.PostAsJsonAsync("/soirees/open/picker/roulette", new { variante = "Aleatoire" }, Ct));

        var responses = await Task.WhenAll(Enumerable.Range(0, Simultanees).Select(_ =>
            client.PostAsJsonAsync("/soirees/open/picker/validate", new { format = 3, fearless = false, alternateSides = true }, Ct)));

        UneSeuleReussit(responses, HttpStatusCode.Created);
        var active = await ReadJsonAsync(await client.GetAsync("/series/active", Ct));
        Assert.Single(active.GetProperty("series").EnumerateArray());
    }
}
