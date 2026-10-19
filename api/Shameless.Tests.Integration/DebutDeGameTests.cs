using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shameless.Application.BackgroundJobs;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Le Début de la Game (ADR 0017) : le signal « partie lancée » du Companion (code Companion) ou, en
/// secours, de l'organisateur. Il renseigne <c>StartedAt</c> (vide avant), émet <c>game_started</c> une
/// seule fois et n'est accepté que pour une game en attente.
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
    public async Task Le_signal_du_companion_renseigne_le_debut_et_emet_game_started()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId);
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        Assert.Equal(JsonValueKind.Null, (await GameDuBotAsync(gameId)).GetProperty("startedAt").ValueKind);

        var reponse = await SignalerDebutAsync(code);

        var corps = await ReadJsonAsync(reponse);
        Assert.Equal(gameId, corps.GetProperty("gameId").GetInt32());
        Assert.Equal(serie.SeriesId, corps.GetProperty("seriesId").GetInt32());
        Assert.False(reponse.Headers.Contains("Set-Cookie"));
        var debut = (await GameDuBotAsync(gameId)).GetProperty("startedAt").GetDateTime();
        Assert.Equal(Factory.Clock.GetUtcNow().UtcDateTime, debut);
        await PublierAsync();
        Assert.Equal(1, NbGameStarted());
    }

    [Fact]
    public async Task Un_second_signal_repond_le_meme_debut_sans_reemettre()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayGameAsync(client, serie.SeriesId);
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        var premier = await ReadJsonAsync(await SignalerDebutAsync(code));
        await PublierAsync();
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));

        var second = await ReadJsonAsync(await SignalerDebutAsync(code));
        await PublierAsync();

        Assert.Equal(premier.GetProperty("startedAt").GetDateTime(), second.GetProperty("startedAt").GetDateTime());
        Assert.Equal(1, NbGameStarted());
    }

    [Fact]
    public async Task Le_signal_est_refuse_sans_game_en_attente()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        await AssertRefusAsync(await SignalerDebutAsync(code), HttpStatusCode.Conflict);

        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await AssertRefusAsync(await SignalerDebutAsync(code), HttpStatusCode.Conflict);
        await PublierAsync();
        // Seul le début de la game terminée à la main est émis (résultat sans signal de début) ; le refus n'émet rien.
        Assert.Equal(1, NbGameStarted());
    }

    [Fact]
    public async Task Un_code_invalide_ou_renouvele_est_refuse_et_ne_signale_rien()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayGameAsync(client, serie.SeriesId);
        var ancien = await CodeCompanionAsync(client, serie.SeriesId);
        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/code-companion/renouveler", null, Ct));

        await AssertRefusAsync(await SignalerDebutAsync("code-inconnu"), HttpStatusCode.NotFound);
        await AssertRefusAsync(await SignalerDebutAsync(ancien), HttpStatusCode.NotFound);

        await PublierAsync();
        Assert.Equal(0, NbGameStarted());
    }

    [Fact]
    public async Task L_organisateur_peut_signaler_le_debut_a_la_main_et_c_est_idempotent()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId);

        var premier = await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/debut", null, Ct));
        var second = await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/debut", null, Ct));

        Assert.Equal(gameId, premier.GetProperty("gameId").GetInt32());
        Assert.Equal(premier.GetProperty("startedAt").GetDateTime(), second.GetProperty("startedAt").GetDateTime());
        Assert.NotEqual(JsonValueKind.Null, (await GameDuBotAsync(gameId)).GetProperty("startedAt").ValueKind);
        await PublierAsync();
        Assert.Equal(1, NbGameStarted());
    }

    [Fact]
    public async Task Le_signal_manuel_est_refuse_sans_game_en_attente_et_sans_session()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);

        await AssertRefusAsync(await client.PostAsync($"/series/{serie.SeriesId}/debut", null, Ct), HttpStatusCode.Conflict);
        await AssertRefusAsync(await client.PostAsync("/series/999/debut", null, Ct), HttpStatusCode.NotFound);
        var anonyme = await Factory.CreateAnonymousClient().PostAsync($"/series/{serie.SeriesId}/debut", null, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonyme.StatusCode);
    }

    [Fact]
    public async Task Une_serie_interrompue_emporte_le_game_started_de_sa_game_en_attente()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayGameAsync(client, serie.SeriesId);
        await ReadJsonAsync(await SignalerDebutAsync(await CodeCompanionAsync(client, serie.SeriesId)));
        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));

        await PublierAsync();

        // Reste le début de la première game, terminée à la main : celui de la game en attente est emporté.
        Assert.Equal(1, NbGameStarted());
    }

    private IEnumerable<string?> TypesEmis() =>
        Factory.Bot.Webhooks.Select(w => JsonDocument.Parse(w.Body).RootElement.GetProperty("type").GetString())
            .Where(t => t is "game_started" or "game_completed");

    [Fact]
    public async Task Un_resultat_saisi_sans_signal_de_debut_renseigne_le_debut_et_emet_game_started_avant_game_completed()
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
    public async Task Un_debut_deja_signale_n_est_ni_ecrase_ni_reemis_a_la_fin_de_la_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId);
        var debut = Factory.Clock.GetUtcNow().UtcDateTime;
        await ReadJsonAsync(await SignalerDebutAsync(await CodeCompanionAsync(client, serie.SeriesId)));
        Factory.Clock.Advance(TimeSpan.FromMinutes(30));

        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "A" }, Ct));

        Assert.Equal(debut, (await GameDuBotAsync(gameId)).GetProperty("startedAt").GetDateTime());
        await PublierAsync();
        Assert.Equal(["game_started", "game_completed"], TypesEmis());
    }
}
