using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shameless.Application.BackgroundJobs;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// Les webhooks API → bot (ADR 0013) : <c>teams_formed</c> à la création d'une Série (validation du
/// Picker), <c>game_started</c> au lancement d'une Game, <c>game_completed</c> à sa fin. Chaque
/// événement est enregistré avec la commande qui le produit, puis livré par le job de publication :
/// enveloppe v1, <c>event_id</c> stable, signature HMAC refaite à chaque tentative, retentée sur 409,
/// 5xx et panne réseau.
/// </summary>
public class WebhooksDuBotTests(ApiFactory factory) : IntegrationTest(factory)
{
    private Task PublierAsync() =>
        Factory.Services.GetServices<IHostedService>().OfType<PublicationWebhooksBotJob>().Single().PublierAsync();

    /// <summary>L'enveloppe d'un webhook reçu, après vérification de sa signature et de son timestamp.</summary>
    private JsonElement Enveloppe(FakeBot.Requete webhook)
    {
        Assert.Equal("POST", webhook.Method);
        Assert.Equal(Factory.Clock.GetUtcNow().ToUnixTimeSeconds().ToString(), webhook.Timestamp);
        var signe = Encoding.UTF8.GetBytes(webhook.Timestamp + ".").Concat(webhook.Body).ToArray();
        var attendue = "sha256=" + Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(FakeBot.WebhookSecret), signe));
        Assert.Equal(attendue, webhook.Signature);
        return JsonDocument.Parse(webhook.Body).RootElement;
    }

    /// <summary>Les requêtes reçues d'un type d'événement, tentatives comprises.</summary>
    private List<FakeBot.Requete> Recus(string type) =>
        Factory.Bot.Webhooks.Where(w => JsonDocument.Parse(w.Body).RootElement.GetProperty("type").GetString() == type).ToList();

    private List<JsonElement> Envoyes(string type) =>
        Factory.Bot.Webhooks.Select(w => JsonDocument.Parse(w.Body).RootElement)
            .Where(e => e.GetProperty("type").GetString() == type)
            .ToList();

    [Fact]
    public async Task La_creation_d_une_serie_envoie_teams_formed_signe_avec_les_ids_discord_des_equipes()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await LierADiscordAsync(serie.TeamA[0], "111111111111111111");

        await PublierAsync();

        var enveloppe = Enveloppe(Factory.Bot.Webhooks.Single(w => w.Texte.Contains("\"teams_formed\"")));
        Assert.Equal(1, enveloppe.GetProperty("version").GetInt32());
        Assert.Equal("teams_formed", enveloppe.GetProperty("type").GetString());
        Assert.Equal($"teams_formed:series:{serie.SeriesId}", enveloppe.GetProperty("event_id").GetString());
        Assert.EndsWith("Z", enveloppe.GetProperty("occurred_at").GetString());
        var data = enveloppe.GetProperty("data");
        Assert.Equal(serie.SeriesId.ToString(), data.GetProperty("series_id").GetString());
        Assert.Equal("Équipe A", data.GetProperty("team_a").GetProperty("name").GetString());
        var joueursA = data.GetProperty("team_a").GetProperty("players").EnumerateArray().ToList();
        Assert.Equal(5, joueursA.Count);
        Assert.Equal("111111111111111111", joueursA[0].GetProperty("discord_id").GetString());
        Assert.Equal("A1", joueursA[0].GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, joueursA[1].GetProperty("discord_id").ValueKind);
        Assert.Equal(5, data.GetProperty("team_b").GetProperty("players").GetArrayLength());
    }

    private async Task<JsonElement> LiensAsync(HttpClient client, int seriesId) =>
        await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}/liens", Ct));

    private async Task DesignerAsync(HttpClient client, int seriesId, string equipe, int playerId) =>
        await ReadJsonAsync(await client.PutAsJsonAsync($"/series/{seriesId}/capitaines/{equipe}",
            new { designation = "Manuel", playerId }, Ct));

    [Fact]
    public async Task La_creation_d_une_serie_envoie_un_lien_par_capitaine_et_le_lien_spectateur()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await DesignerAsync(client, serie.SeriesId, "A", serie.TeamA[2]);
        await DesignerAsync(client, serie.SeriesId, "B", serie.TeamB[4]);
        await LierADiscordAsync(serie.TeamA[2], "111111111111111111");
        var liens = await LiensAsync(client, serie.SeriesId);

        await PublierAsync();

        var capitaines = Envoyes("captain_link");
        Assert.Equal(2, capitaines.Count);
        var a = capitaines.Single(e => e.GetProperty("data").GetProperty("team").GetString() == "A");
        Assert.Equal($"captain_link:series:{serie.SeriesId}:team:A", a.GetProperty("event_id").GetString());
        var dataA = a.GetProperty("data");
        Assert.Equal(serie.SeriesId.ToString(), dataA.GetProperty("series_id").GetString());
        Assert.Equal("Équipe A", dataA.GetProperty("team_name").GetString());
        Assert.Equal("A3", dataA.GetProperty("captain").GetProperty("name").GetString());
        Assert.Equal("111111111111111111", dataA.GetProperty("captain").GetProperty("discord_id").GetString());
        Assert.Equal($"{TestSession.AllowedOrigin}/draft/capitaine/{liens.GetProperty("lienCapitaineA").GetString()}",
            dataA.GetProperty("url").GetString());

        var b = capitaines.Single(e => e.GetProperty("data").GetProperty("team").GetString() == "B");
        Assert.Equal($"captain_link:series:{serie.SeriesId}:team:B", b.GetProperty("event_id").GetString());
        Assert.Equal(JsonValueKind.Null, b.GetProperty("data").GetProperty("captain").GetProperty("discord_id").ValueKind);
        Assert.Equal($"{TestSession.AllowedOrigin}/draft/capitaine/{liens.GetProperty("lienCapitaineB").GetString()}",
            b.GetProperty("data").GetProperty("url").GetString());

        var spectateur = Assert.Single(Envoyes("spectator_link"));
        Assert.Equal($"spectator_link:series:{serie.SeriesId}", spectateur.GetProperty("event_id").GetString());
        Assert.Equal(serie.SeriesId.ToString(), spectateur.GetProperty("data").GetProperty("series_id").GetString());
        Assert.Equal($"{TestSession.AllowedOrigin}/draft/spectateur/{liens.GetProperty("lienSpectateur").GetString()}",
            spectateur.GetProperty("data").GetProperty("url").GetString());
    }

    [Fact]
    public async Task Le_lien_d_une_equipe_sans_capitaine_attend_sa_designation_pour_partir()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PublierAsync();
        Assert.Empty(Envoyes("captain_link"));
        Assert.Single(Envoyes("spectator_link"));

        await DesignerAsync(client, serie.SeriesId, "A", serie.TeamA[0]);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        await PublierAsync();

        var envoye = Assert.Single(Envoyes("captain_link"));
        Assert.Equal("A", envoye.GetProperty("data").GetProperty("team").GetString());
        Assert.Equal("A1", envoye.GetProperty("data").GetProperty("captain").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Sans_bot_lie_aucun_evenement_n_est_emis_et_les_liens_restent_copiables()
    {
        await using var sansBot = Factory.WithWebHostBuilder(b => b.UseSetting("Bot:Url", ""));
        var client = sansBot.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        await TestSession.CompleteLoginAsync(client, Factory.Discord.AddUser("1001", "joueur", TestSession.GuildId));
        var serie = await CreateSerieAsync(client, format: 3);
        await DesignerAsync(client, serie.SeriesId, "A", serie.TeamA[0]);

        await sansBot.Services.GetServices<IHostedService>().OfType<PublicationWebhooksBotJob>().Single().PublierAsync(Ct);

        Assert.Empty(Factory.Bot.Webhooks);
        Assert.False(string.IsNullOrEmpty((await LiensAsync(client, serie.SeriesId)).GetProperty("lienSpectateur").GetString()));
    }

    [Fact]
    public async Task La_validation_du_picker_envoie_teams_formed()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var dix = await CreatePlayersAsync(client, "J1", "J2", "J3", "J4", "J5", "J6", "J7", "J8", "J9", "J10");
        await ReadJsonAsync(await client.PutAsJsonAsync("/soirees/open/picker/selection", new { playerIds = dix }, Ct));
        await ReadJsonAsync(await client.PostAsJsonAsync("/soirees/open/picker/roulette", new { variante = "Aleatoire" }, Ct));
        var serie = await ReadJsonAsync(await client.PostAsJsonAsync("/soirees/open/picker/validate",
            new { format = 3, fearless = false }, Ct));

        await PublierAsync();

        var enveloppe = Assert.Single(Envoyes("teams_formed"));
        Assert.Equal($"teams_formed:series:{serie.GetProperty("seriesId").GetInt32()}", enveloppe.GetProperty("event_id").GetString());
    }

    [Fact]
    public async Task La_fin_de_la_draft_envoie_game_started_avec_ses_participants()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await LierADiscordAsync(serie.TeamA[0], "111111111111111111");
        await LierADiscordAsync(serie.TeamB[1], "222222222222222222");
        await PublierAsync();
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "red");
        await PublierAsync();
        Assert.Empty(Envoyes("game_started"));
        await JouerLaDraftAsync(client, serie.SeriesId, gameId);

        await PublierAsync();

        var enveloppe = Enveloppe(Factory.Bot.Webhooks[^1]);
        Assert.Equal("game_started", enveloppe.GetProperty("type").GetString());
        Assert.Equal($"game_started:game:{gameId}", enveloppe.GetProperty("event_id").GetString());
        var data = enveloppe.GetProperty("data");
        Assert.Equal(serie.SeriesId.ToString(), data.GetProperty("series_id").GetString());
        Assert.Equal(gameId.ToString(), data.GetProperty("game_id").GetString());
        Assert.Equal(1, data.GetProperty("game_number").GetInt32());
        Assert.Equal(3, data.GetProperty("best_of").GetInt32());
        Assert.Equal($"{TestSession.AllowedOrigin}/series/{serie.SeriesId}", data.GetProperty("series_url").GetString());
        Assert.Equal("red", data.GetProperty("team_a").GetProperty("side").GetString());
        Assert.Equal("blue", data.GetProperty("team_b").GetProperty("side").GetString());
        Assert.Equal("A1", data.GetProperty("team_a").GetProperty("players")[0].GetProperty("name").GetString());
        Assert.Equal(["111111111111111111", "222222222222222222"],
            data.GetProperty("participant_discord_ids").EnumerateArray().Select(i => i.GetString()));
    }

    [Fact]
    public async Task La_fin_d_une_game_envoie_game_completed_avec_vainqueur_score_stats_et_lien_de_recap()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await LierADiscordAsync(serie.TeamA[0], "111111111111111111");
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await PublierAsync();
        object StatsDe(int i) => new
        {
            kills = 10, deaths = 2, assists = 5, totalMinionsKilled = 200, neutralMinionsKilled = 40,
            totalDamageDealtToChampions = 25000 + i, teamPosition = "TOP"
        };
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_8000000001", "blue", "A", statsDe: StatsDe,
            donnees: new { gameDuration = 1800, gameMode = "CLASSIC", gameType = "CUSTOM_GAME" })));

        await PublierAsync();

        var enveloppe = Enveloppe(Factory.Bot.Webhooks[^1]);
        Assert.Equal("game_completed", enveloppe.GetProperty("type").GetString());
        Assert.Equal($"game_completed:game:{gameId}", enveloppe.GetProperty("event_id").GetString());
        var data = enveloppe.GetProperty("data");
        Assert.Equal("A", data.GetProperty("winner").GetString());
        Assert.Equal(1, data.GetProperty("series_score").GetProperty("A").GetInt32());
        Assert.Equal(0, data.GetProperty("series_score").GetProperty("B").GetInt32());
        Assert.Equal(1800, data.GetProperty("duration_seconds").GetInt32());
        Assert.Equal($"{TestSession.AllowedOrigin}/games/{gameId}", data.GetProperty("recap_url").GetString());
        Assert.Equal(["111111111111111111"], data.GetProperty("participant_discord_ids").EnumerateArray().Select(i => i.GetString()));
        var a1 = data.GetProperty("team_a").GetProperty("players")[0];
        Assert.Equal("111111111111111111", a1.GetProperty("discord_id").GetString());
        Assert.Equal("Garen", a1.GetProperty("champion").GetString());
        Assert.Equal("TOP", a1.GetProperty("role").GetString());
        Assert.Equal(10, a1.GetProperty("kills").GetInt32());
        Assert.Equal(2, a1.GetProperty("deaths").GetInt32());
        Assert.Equal(5, a1.GetProperty("assists").GetInt32());
        Assert.Equal(240, a1.GetProperty("cs").GetInt32());
        Assert.Equal(8.0, a1.GetProperty("cs_per_min").GetDouble());
        Assert.Equal(0.3, a1.GetProperty("kill_participation").GetDouble());
        Assert.Equal(25000, a1.GetProperty("damage").GetInt32());
        Assert.InRange(a1.GetProperty("performance_score").GetInt32(), 0, 100);
    }

    [Fact]
    public async Task Une_game_saisie_a_la_main_envoie_game_completed_sans_stats()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "B");

        await PublierAsync();

        var data = Assert.Single(Envoyes("game_completed")).GetProperty("data");
        Assert.Equal(gameId.ToString(), data.GetProperty("game_id").GetString());
        Assert.Equal("B", data.GetProperty("winner").GetString());
        Assert.False(data.TryGetProperty("duration_seconds", out _));
        var a1 = data.GetProperty("team_a").GetProperty("players")[0];
        Assert.Equal("A1", a1.GetProperty("name").GetString());
        Assert.False(a1.TryGetProperty("kills", out _));
    }

    [Fact]
    public async Task Un_webhook_livre_n_est_plus_renvoye()
    {
        var client = Factory.CreateSignedInClient();
        await CreateSerieAsync(client);

        await PublierAsync();
        await PublierAsync();

        Assert.Single(Recus("teams_formed"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task Un_webhook_en_cours_ou_en_echec_cote_bot_est_retente_plus_tard_avec_le_meme_event_id_et_une_signature_fraiche(
        HttpStatusCode statut)
    {
        var client = Factory.CreateSignedInClient();
        await CreateSerieAsync(client);
        Factory.Bot.RepondreAuWebhook(FakeBot.Statut(statut, """{"detail":"in_progress"}"""));

        await PublierAsync();
        await PublierAsync(); // pas encore l'heure de retenter
        Assert.Single(Recus("teams_formed"));

        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await PublierAsync();

        var (premier, second) = (Recus("teams_formed")[0], Recus("teams_formed")[1]);
        Assert.Equal(2, Recus("teams_formed").Count);
        Assert.NotEqual(premier.Timestamp, second.Timestamp);
        var reessai = Enveloppe(second);
        Assert.Equal(JsonDocument.Parse(premier.Body).RootElement.GetProperty("event_id").GetString(),
            reessai.GetProperty("event_id").GetString());

        await PublierAsync();
        Factory.Clock.Advance(TimeSpan.FromHours(1));
        await PublierAsync();
        Assert.Equal(2, Recus("teams_formed").Count);
    }

    [Fact]
    public async Task Un_bot_injoignable_fait_retenter_le_webhook()
    {
        var client = Factory.CreateSignedInClient();
        await CreateSerieAsync(client);
        Factory.Bot.RepondreAuWebhook(FakeBot.Panne());

        await PublierAsync();
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await PublierAsync();

        Assert.Equal(2, Recus("teams_formed").Count);
    }

    [Fact]
    public async Task Un_webhook_refuse_definitivement_par_le_bot_n_est_pas_retente()
    {
        var client = Factory.CreateSignedInClient();
        await CreateSerieAsync(client);
        Factory.Bot.RepondreAuWebhook(FakeBot.Statut(HttpStatusCode.UnprocessableEntity));

        await PublierAsync();
        Factory.Clock.Advance(TimeSpan.FromHours(1));
        await PublierAsync();

        Assert.Single(Recus("teams_formed"));
    }

    [Fact]
    public async Task Les_webhooks_sont_livres_dans_l_ordre_des_evenements()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId);
        await JouerLaDraftAsync(client, serie.SeriesId, gameId);
        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "A" }, Ct));

        await PublierAsync();

        Assert.Equal(["teams_formed", "captain_link", "captain_link", "spectator_link", "game_started", "game_completed"],
            Factory.Bot.Webhooks.Select(w => JsonDocument.Parse(w.Body).RootElement.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task Une_serie_annulee_avant_publication_n_envoie_rien()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayGameAsync(client, serie.SeriesId);
        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));

        await PublierAsync();

        Assert.Empty(Factory.Bot.Webhooks);
    }

    [Fact]
    public async Task Une_serie_annulee_emporte_son_game_started_non_livre()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId);
        await JouerLaDraftAsync(client, serie.SeriesId, gameId);
        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));

        await PublierAsync();

        Assert.Empty(Factory.Bot.Webhooks);
    }
}
