using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Le rattachement manuel des Clips (ADR 0014, #64) : un Utilisateur connecté consulte les clips sans
/// Game, en rattache un à une Game, corrige ou retire un rattachement. La Game lit aussitôt ses clips.
/// </summary>
public class ClipsManuelsTests(ApiFactory factory) : IntegrationTest(factory)
{
    // Samedi 12 septembre 2026 : 18 h UTC = 20 h à Paris.
    private static readonly DateTimeOffset Soir = new(2026, 9, 12, 18, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Video = [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6d, 0x70, 0x34, 0x32];

    private async Task<JsonElement> DeposerAsync(string attachmentId, DateTimeOffset publieLe, string nomFichier = "pentakill.mp4")
    {
        var fichier = new ByteArrayContent(Video);
        fichier.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        var clip = new MultipartFormDataContent
        {
            { new StringContent("1300000000000000001"), "messageId" },
            { new StringContent(attachmentId), "attachmentId" },
            { new StringContent("111111111111111111"), "auteurDiscordId" },
            { new StringContent("Sam"), "auteurNom" },
            { new StringContent(publieLe.ToString("O")), "publieLe" },
            { fichier, "fichier", nomFichier }
        };
        return await ReadJsonAsync(await Factory.CreateBotClient().PostAsync("/bot/clips", clip, Ct));
    }

    private static int Id(JsonElement clip) => clip.GetProperty("clipId").GetInt32();

    /// <summary>Connecte l'organisateur puis règle l'horloge sur <see cref="Soir"/> (le cookie reste valide).</summary>
    private HttpClient OrganisateurLeSoir()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Clock.SetUtcNow(Soir);
        return client;
    }

    private async Task<List<int>> ClipsDeLaGameAsync(HttpClient client, int gameId) =>
        (await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/clips", Ct)))
        .EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).ToList();

    private async Task<List<int>> ClipsSansGameAsync(HttpClient client) =>
        (await ReadJsonAsync(await client.GetAsync("/clips/sans-game", Ct)))
        .EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).ToList();

    [Fact]
    public async Task Les_clips_sans_game_se_consultent_du_plus_ancien_au_plus_recent()
    {
        var client = OrganisateurLeSoir();
        var serie = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        var rattache = await DeposerAsync("1300000000000000201", Soir.AddMinutes(30));
        var tard = await DeposerAsync("1300000000000000202", Soir.AddHours(5), "baron.mp4");
        var avant = await DeposerAsync("1300000000000000203", Soir.AddHours(-30), "dragon.mp4");

        var clips = await ClipsSansGameAsync(client);

        Assert.Equal([Id(avant), Id(tard)], clips);
        Assert.DoesNotContain(Id(rattache), clips);
    }

    [Fact]
    public async Task Un_clip_sans_game_se_rattache_a_une_game_et_la_game_le_lit_aussitot()
    {
        var client = OrganisateurLeSoir();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        Factory.Clock.Advance(TimeSpan.FromMinutes(40));
        var game2 = await PlayAndWinAsync(client, serie.SeriesId, "B");
        var clip = await DeposerAsync("1300000000000000211", Soir.AddHours(2));

        var response = await client.PutAsJsonAsync($"/clips/{Id(clip)}/game", new { gameId = game2 }, Ct);

        var rattache = await ReadJsonAsync(response);
        Assert.Equal(game2, rattache.GetProperty("gameId").GetInt32());
        Assert.Equal("manuel", rattache.GetProperty("rattachement").GetString());
        Assert.Equal([Id(clip)], await ClipsDeLaGameAsync(client, game2));
        Assert.Empty(await ClipsSansGameAsync(client));
    }

    [Fact]
    public async Task Un_clip_rattache_a_la_mauvaise_game_se_corrige()
    {
        var client = OrganisateurLeSoir();
        var serie = await CreateSerieAsync(client, format: 3);
        var game1 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        var clip = await DeposerAsync("1300000000000000221", Soir.AddMinutes(30));
        Factory.Clock.Advance(TimeSpan.FromMinutes(40));
        var game2 = await PlayAndWinAsync(client, serie.SeriesId, "B");
        Assert.Equal(game1, clip.GetProperty("gameId").GetInt32());

        var corrige = await ReadJsonAsync(await client.PutAsJsonAsync($"/clips/{Id(clip)}/game", new { gameId = game2 }, Ct));

        Assert.Equal("manuel", corrige.GetProperty("rattachement").GetString());
        Assert.Empty(await ClipsDeLaGameAsync(client, game1));
        Assert.Equal([Id(clip)], await ClipsDeLaGameAsync(client, game2));
    }

    [Fact]
    public async Task Un_rattachement_se_retire_et_le_clip_revient_parmi_les_clips_sans_game()
    {
        var client = OrganisateurLeSoir();
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");
        var clip = await DeposerAsync("1300000000000000231", Soir.AddMinutes(30));

        var retire = await ReadJsonAsync(await client.DeleteAsync($"/clips/{Id(clip)}/game", Ct));

        Assert.Equal(JsonValueKind.Null, retire.GetProperty("gameId").ValueKind);
        Assert.Equal(JsonValueKind.Null, retire.GetProperty("rattachement").ValueKind);
        Assert.Empty(await ClipsDeLaGameAsync(client, gameId));
        Assert.Equal([Id(clip)], await ClipsSansGameAsync(client));
    }

    [Fact]
    public async Task Les_games_proposees_pour_un_clip_sont_celles_lancees_la_veille_et_le_jour_de_publication_avant_lui()
    {
        var client = OrganisateurLeSoir();
        Factory.Clock.SetUtcNow(Soir.AddDays(-3));
        var serie = await CreateSerieAsync(client, format: 5);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        Factory.Clock.SetUtcNow(Soir);
        var game1 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        Factory.Clock.Advance(TimeSpan.FromMinutes(40));
        var game2 = await PlayAndWinAsync(client, serie.SeriesId, "B");
        // 22 h 30 UTC = 0 h 30 à Paris, le lendemain des games : le clip reste sans Game.
        var clip = await DeposerAsync("1300000000000000251", Soir.AddHours(4.5));
        Factory.Clock.SetUtcNow(Soir.AddHours(5));
        await PlayGameAsync(client, serie.SeriesId);

        var candidates = (await ReadJsonAsync(await client.GetAsync($"/clips/{Id(clip)}/games-candidates", Ct))).EnumerateArray().ToList();

        Assert.Equal([game2, game1], candidates.Select(g => g.GetProperty("gameId").GetInt32()));
        Assert.Equal(3, candidates[0].GetProperty("gameNumber").GetInt32());
        Assert.Equal(serie.SeriesId, candidates[0].GetProperty("seriesId").GetInt32());
        Assert.Equal(5, candidates[0].GetProperty("bestOf").GetInt32());
        Assert.Equal(Soir.AddMinutes(40), candidates[0].GetProperty("lanceeLe").GetDateTimeOffset());
        await AssertRefusAsync(await client.GetAsync("/clips/999999/games-candidates", Ct), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rattacher_a_une_game_inconnue_ou_un_clip_inconnu_repond_404_sans_rien_changer()
    {
        var client = OrganisateurLeSoir();
        var clip = await DeposerAsync("1300000000000000241", Soir);

        await AssertRefusAsync(await client.PutAsJsonAsync($"/clips/{Id(clip)}/game", new { gameId = 999999 }, Ct), HttpStatusCode.NotFound);
        await AssertRefusAsync(await client.PutAsJsonAsync("/clips/999999/game", new { gameId = 1 }, Ct), HttpStatusCode.NotFound);
        await AssertRefusAsync(await client.DeleteAsync("/clips/999999/game", Ct), HttpStatusCode.NotFound);
        Assert.Equal([Id(clip)], await ClipsSansGameAsync(client));
    }
}
