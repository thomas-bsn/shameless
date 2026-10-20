using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Plusieurs Comptes Discord pour un même Joueur (ADR 0018) : sans vérification, le premier lié est le
/// Discord principal (photo et identité affichées), chacun voit son Joueur, et tous comptent comme lui.
/// </summary>
public class PlusieursDiscordTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const string PhotoPremier = "https://cdn.discordapp.com/avatars/8001/photo1.png";
    private const string PhotoSecond = "https://cdn.discordapp.com/avatars/8002/photo2.png";

    private async Task<HttpClient> ConnecterAsync(string discordId, string username, string avatar)
    {
        var client = Factory.CreateAnonymousClient();
        var code = Factory.Discord.AddProfile(discordId, username, avatar, null, null, TestSession.GuildId);
        await TestSession.CompleteLoginAsync(client, code);
        return client;
    }

    private async Task<(HttpClient Premier, HttpClient Second, int PlayerId)> DeuxLiesAsync()
    {
        var premier = await ConnecterAsync("8001", "premier", "photo1");
        var playerId = await CreatePlayerAsync(premier, "Alice");
        await ReadJsonAsync(await premier.PostAsJsonAsync("/players/link", new { playerId }, Ct));
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        var second = await ConnecterAsync("8002", "second", "photo2");
        await ReadJsonAsync(await second.PostAsJsonAsync("/players/link", new { playerId }, Ct));
        return (premier, second, playerId);
    }

    private static JsonElement Ligne(JsonElement lignes, int id) =>
        lignes.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == id);

    [Fact]
    public async Task Deux_utilisateurs_lient_le_meme_joueur_et_chacun_le_voit_sur_players_me()
    {
        var (premier, second, playerId) = await DeuxLiesAsync();

        foreach (var client in new[] { premier, second })
        {
            var me = await ReadJsonAsync(await client.GetAsync("/players/me", Ct));
            Assert.Equal(playerId, me.GetProperty("id").GetInt32());
        }
    }

    [Fact]
    public async Task Le_premier_discord_lie_reste_le_principal_pour_la_photo()
    {
        var (premier, second, playerId) = await DeuxLiesAsync();

        var joueurs = await second.GetFromJsonAsync<JsonElement>("/players", Ct);
        Assert.Equal(PhotoPremier, Ligne(joueurs, playerId).GetProperty("avatarUrl").GetString());
        var me = await ReadJsonAsync(await second.GetAsync("/players/me", Ct));
        Assert.Equal(PhotoPremier, me.GetProperty("avatarUrl").GetString());

        var fiche = await premier.GetFromJsonAsync<JsonElement>($"/players/{playerId}/fiche", Ct);
        Assert.Equal("8001", fiche.GetProperty("discord").GetProperty("discordId").GetString());
        var comptes = fiche.GetProperty("comptesDiscord").EnumerateArray().ToList();
        Assert.Equal(["8001", "8002"], comptes.Select(c => c.GetProperty("discordId").GetString()));
        Assert.Equal([true, false], comptes.Select(c => c.GetProperty("principal").GetBoolean()));
        Assert.Equal(PhotoSecond, comptes[1].GetProperty("avatarUrl").GetString());
    }

    [Fact]
    public async Task Delier_le_principal_promeut_le_suivant_sans_toucher_au_lien_de_l_autre()
    {
        var (premier, second, playerId) = await DeuxLiesAsync();

        Assert.Equal(HttpStatusCode.OK, (await premier.DeleteAsync("/players/link", Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await premier.GetAsync("/players/me", Ct)).StatusCode);
        var me = await ReadJsonAsync(await second.GetAsync("/players/me", Ct));
        Assert.Equal(playerId, me.GetProperty("id").GetInt32());
        Assert.Equal(PhotoSecond, me.GetProperty("avatarUrl").GetString());
        var fiche = await second.GetFromJsonAsync<JsonElement>($"/players/{playerId}/fiche", Ct);
        Assert.Equal("8002", fiche.GetProperty("discord").GetProperty("discordId").GetString());
        Assert.True(Assert.Single(fiche.GetProperty("comptesDiscord").EnumerateArray()).GetProperty("principal").GetBoolean());
    }

    [Fact]
    public async Task Delier_le_dernier_discord_laisse_le_joueur_sans_photo()
    {
        var (premier, second, playerId) = await DeuxLiesAsync();
        await premier.DeleteAsync("/players/link", Ct);
        await second.DeleteAsync("/players/link", Ct);

        var joueurs = await second.GetFromJsonAsync<JsonElement>("/players", Ct);
        Assert.Equal(JsonValueKind.Null, Ligne(joueurs, playerId).GetProperty("avatarUrl").ValueKind);
    }

    [Fact]
    public async Task Un_discord_deja_lie_a_un_joueur_ne_peut_pas_en_lier_un_autre()
    {
        var (_, second, playerId) = await DeuxLiesAsync();
        var autre = await CreatePlayerAsync(second, "Bob");

        var refus = await second.PostAsJsonAsync("/players/link", new { playerId = autre }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, refus.StatusCode);
        var me = await ReadJsonAsync(await second.GetAsync("/players/me", Ct));
        Assert.Equal(playerId, me.GetProperty("id").GetInt32());
        // Relier son propre Joueur reste sans effet.
        Assert.True((await second.PostAsJsonAsync("/players/link", new { playerId }, Ct)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Tous_les_discord_d_un_joueur_comptent_comme_lui_pour_les_pronostics_et_les_votes()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await LierADiscordAsync(serie.TeamA[0], "100000000000000001");
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await LierADiscordAsync(serie.TeamA[0], "100000000000000099");
        var bot = Factory.CreateBotClient();

        var pronostic = await bot.PutAsJsonAsync($"/bot/series/{serie.SeriesId}/pronostics",
            new { pronostiqueurDiscordId = "100000000000000099", gameNumber = 1, equipeGagnante = "A" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, pronostic.StatusCode);

        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");
        var vote = await bot.PutAsJsonAsync($"/bot/games/{gameId}/votes",
            new { votantDiscordId = "100000000000000099", dofdPlayerId = serie.TeamA[0] }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, vote.StatusCode); // pour soi-même
        var voteAutre = await bot.PutAsJsonAsync($"/bot/games/{gameId}/votes",
            new { votantDiscordId = "100000000000000099", dofdPlayerId = serie.TeamA[1] }, Ct);
        Assert.Equal(HttpStatusCode.OK, voteAutre.StatusCode); // c'est un Joueur de la game, il peut voter
    }

    [Fact]
    public async Task Le_picker_vocal_propose_le_joueur_une_seule_fois_quel_que_soit_son_discord_present()
    {
        var organisateur = Factory.CreateSignedInClient();
        var zed = await CreatePlayerAsync(organisateur, "Zed");
        await LierADiscordAsync(zed, "300000000000000001");
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await LierADiscordAsync(zed, "300000000000000002");
        Factory.Bot.CandidatsVocaux = () => FakeBot.Json(
            """{"channel_id":"900000000000000009","member_ids":["300000000000000002"]}""");

        var candidats = await ReadJsonAsync(await organisateur.GetAsync("/soirees/open/picker/candidats-vocaux", Ct));

        var joueur = Assert.Single(candidats.GetProperty("joueurs").EnumerateArray());
        Assert.Equal(zed, joueur.GetProperty("playerId").GetInt32());
        Assert.Equal(0, candidats.GetProperty("membresNonLies").GetInt32());
    }
}
