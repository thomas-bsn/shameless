using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Partout où un Joueur apparaît (Série, game, draft de champions, historique, votes), la réponse expose la photo
/// Discord de son compte lié ; les pages publiques (Lien spectateur) la voient sans connexion.
/// </summary>
public class AvatarsPartoutTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const string Photo = "https://cdn.discordapp.com/avatars/6002/photo1.png";

    private async Task<(HttpClient Client, CreatedSerie Serie)> SerieAvecUnJoueurLieAsync()
    {
        var client = Factory.CreateAnonymousClient();
        var code = Factory.Discord.AddProfile("6002", "lou", "photo1", null, null, TestSession.GuildId);
        await TestSession.CompleteLoginAsync(client, code);
        var serie = await CreateSerieAsync(client);
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/link", new { playerId = serie.TeamA[0] }, Ct));
        return (client, serie);
    }

    private static JsonElement Joueur(JsonElement equipe, int playerId) =>
        equipe.EnumerateArray().Single(p => p.GetProperty("playerId").GetInt32() == playerId);

    private static void AssertPhotoEtSansPhoto(JsonElement equipeA, JsonElement equipeB, CreatedSerie serie)
    {
        Assert.Equal(Photo, Joueur(equipeA, serie.TeamA[0]).GetProperty("avatarUrl").GetString());
        Assert.Equal(JsonValueKind.Null, Joueur(equipeA, serie.TeamA[1]).GetProperty("avatarUrl").ValueKind);
        Assert.Equal(JsonValueKind.Null, Joueur(equipeB, serie.TeamB[0]).GetProperty("avatarUrl").ValueKind);
    }

    [Fact]
    public async Task La_page_de_la_serie_et_la_game_exposent_la_photo_des_joueurs()
    {
        var (client, serie) = await SerieAvecUnJoueurLieAsync();
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");

        var page = await client.GetFromJsonAsync<JsonElement>($"/series/{serie.SeriesId}", Ct);
        AssertPhotoEtSansPhoto(page.GetProperty("teamA"), page.GetProperty("teamB"), serie);
        var game = page.GetProperty("games")[0];
        AssertPhotoEtSansPhoto(game.GetProperty("teamA"), game.GetProperty("teamB"), serie);

        var recap = await client.GetFromJsonAsync<JsonElement>($"/games/{gameId}", Ct);
        AssertPhotoEtSansPhoto(recap.GetProperty("teamA"), recap.GetProperty("teamB"), serie);
    }

    [Fact]
    public async Task La_draft_de_champions_expose_la_photo_au_lien_spectateur_sans_connexion()
    {
        var (client, serie) = await SerieAvecUnJoueurLieAsync();
        await PlayGameAsync(client, serie.SeriesId);
        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/draft-champions", null, Ct));
        var liens = await client.GetFromJsonAsync<JsonElement>($"/series/{serie.SeriesId}/liens", Ct);
        var spectateur = Uri.EscapeDataString(liens.GetProperty("lienSpectateur").GetString()!);

        var draft = await Factory.CreateAnonymousClient()
            .GetFromJsonAsync<JsonElement>($"/liens/{spectateur}/draft-champions", Ct);

        AssertPhotoEtSansPhoto(draft.GetProperty("teamA"), draft.GetProperty("teamB"), serie);
    }
}
