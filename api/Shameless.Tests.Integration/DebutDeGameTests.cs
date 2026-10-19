using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shameless.Application.BackgroundJobs;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Le Début de la Game (ADR 0017) : la fin de la Draft de champions (dernier pick). Elle renseigne
/// <c>StartedAt</c> (vide avant), émet <c>game_started</c> une seule fois et clôt les pronostics.
/// </summary>
public class DebutDeGameTests(ApiFactory factory) : IntegrationTest(factory)
{
    private Task PublierAsync() =>
        Factory.Services.GetServices<IHostedService>().OfType<PublicationWebhooksBotJob>().Single().PublierAsync();

    private int NbGameStarted() =>
        Factory.Bot.Webhooks.Count(w => JsonDocument.Parse(w.Body).RootElement.GetProperty("type").GetString() == "game_started");

    private async Task<JsonElement> GameDuBotAsync(int gameId) =>
        await ReadJsonAsync(await Factory.CreateBotClient().GetAsync($"/bot/games/{gameId}", Ct));

    [Fact]
    public async Task Le_dernier_pick_de_la_draft_renseigne_le_debut_et_emet_game_started()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId);

        // Pendant la draft : le bot lit un début vide, rien n'est émis.
        await JouerLaDraftAsync(client, serie.SeriesId, gameId, jusquau: 19);
        Assert.Equal(JsonValueKind.Null, (await GameDuBotAsync(gameId)).GetProperty("startedAt").ValueKind);
        await PublierAsync();
        Assert.Equal(0, NbGameStarted());

        await JouerLaDraftAsync(client, serie.SeriesId, gameId);

        var debut = (await GameDuBotAsync(gameId)).GetProperty("startedAt").GetDateTime();
        Assert.Equal(Factory.Clock.GetUtcNow().UtcDateTime, debut);
        await PublierAsync();
        Assert.Equal(1, NbGameStarted());
    }

    [Fact]
    public async Task Les_pronostics_se_closent_a_la_fin_de_la_draft_et_pas_avant()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId);
        await JouerLaDraftAsync(client, serie.SeriesId, gameId, jusquau: 19);

        var pendant = await ReadJsonAsync(await Factory.CreateBotClient().GetAsync($"/bot/series/{serie.SeriesId}/pronostics", Ct));
        Assert.Equal(1, pendant.GetProperty("gameNumber").GetInt32());

        await JouerLaDraftAsync(client, serie.SeriesId, gameId);

        var apres = await ReadJsonAsync(await Factory.CreateBotClient().GetAsync($"/bot/series/{serie.SeriesId}/pronostics", Ct));
        Assert.Equal(JsonValueKind.Null, apres.GetProperty("gameNumber").ValueKind);
    }

    [Fact]
    public async Task Le_signal_du_companion_et_la_commande_manuelle_n_existent_plus()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayGameAsync(client, serie.SeriesId);
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        var companion = await Factory.CreateAnonymousClient().PostAsync($"/companion/{Uri.EscapeDataString(code)}/debut", null, Ct);
        var manuel = await client.PostAsync($"/series/{serie.SeriesId}/debut", null, Ct);

        Assert.Contains(companion.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed, HttpStatusCode.Unauthorized });
        Assert.Contains(manuel.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed, HttpStatusCode.Unauthorized });
    }

    [Fact]
    public async Task Une_serie_interrompue_emporte_le_game_started_de_sa_game_en_attente()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        var gameId = await PlayGameAsync(client, serie.SeriesId);
        await JouerLaDraftAsync(client, serie.SeriesId, gameId);
        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));

        await PublierAsync();

        // Reste le début de la première game, terminée à la main : celui de la game en attente est emporté.
        Assert.Equal(1, NbGameStarted());
    }

    private IEnumerable<string?> TypesEmis() =>
        Factory.Bot.Webhooks.Select(w => JsonDocument.Parse(w.Body).RootElement.GetProperty("type").GetString())
            .Where(t => t is "game_started" or "game_completed");

    [Fact]
    public async Task Un_resultat_saisi_avant_la_fin_de_la_draft_renseigne_le_debut_et_emet_game_started_avant_game_completed()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId);
        Factory.Clock.Advance(TimeSpan.FromMinutes(30));

        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "A" }, Ct));

        Assert.Equal(Factory.Clock.GetUtcNow().UtcDateTime, (await GameDuBotAsync(gameId)).GetProperty("startedAt").GetDateTime());
        await PublierAsync();
        Assert.Equal(["game_started", "game_completed"], TypesEmis());
        Assert.Contains(Factory.Bot.Webhooks, w => System.Text.Encoding.UTF8.GetString(w.Body).Contains($"game_started:game:{gameId}"));
    }

    [Fact]
    public async Task Un_debut_deja_donne_par_la_draft_n_est_ni_ecrase_ni_reemis_a_la_fin_de_la_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId);
        var debut = Factory.Clock.GetUtcNow().UtcDateTime;
        await JouerLaDraftAsync(client, serie.SeriesId, gameId);
        Factory.Clock.Advance(TimeSpan.FromMinutes(30));

        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "A" }, Ct));

        Assert.Equal(debut, (await GameDuBotAsync(gameId)).GetProperty("startedAt").GetDateTime());
        await PublierAsync();
        Assert.Equal(["game_started", "game_completed"], TypesEmis());
    }
}
