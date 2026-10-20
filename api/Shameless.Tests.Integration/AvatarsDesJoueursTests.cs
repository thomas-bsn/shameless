using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Les réponses qui listent des Joueurs exposent la photo Discord du compte lié, ou rien pour un Joueur non lié.
/// </summary>
public class AvatarsDesJoueursTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const string Photo = "https://cdn.discordapp.com/avatars/6001/photo1.png";

    private async Task<HttpClient> ConnecterAvecPhotoAsync(string discordId)
    {
        var client = Factory.CreateAnonymousClient();
        var code = Factory.Discord.AddProfile(discordId, "lou", "photo1", null, null, TestSession.GuildId);
        await TestSession.CompleteLoginAsync(client, code);
        return client;
    }

    private static JsonElement Ligne(JsonElement lignes, int id, string cle = "id") =>
        lignes.EnumerateArray().Single(p => p.GetProperty(cle).GetInt32() == id);

    [Fact]
    public async Task La_liste_des_joueurs_et_les_classements_exposent_la_photo_du_compte_lie_ou_rien()
    {
        var client = await ConnecterAvecPhotoAsync("6001");
        var lie = await CreatePlayerAsync(client, "Lie");
        var libre = await CreatePlayerAsync(client, "Libre");
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/link", new { playerId = lie }, Ct));

        var joueurs = await client.GetFromJsonAsync<JsonElement>("/players", Ct);
        Assert.Equal(Photo, Ligne(joueurs, lie).GetProperty("avatarUrl").GetString());
        Assert.Equal(JsonValueKind.Null, Ligne(joueurs, libre).GetProperty("avatarUrl").ValueKind);

        var fiche = await client.GetFromJsonAsync<JsonElement>($"/players/{lie}/fiche", Ct);
        Assert.Equal(Photo, fiche.GetProperty("discord").GetProperty("avatarUrl").GetString());

        var solo = await client.GetFromJsonAsync<JsonElement>("/classements/soloq", Ct);
        Assert.Equal(Photo, Ligne(solo.GetProperty("solo"), lie, "playerId").GetProperty("avatarUrl").GetString());

        var cinqContreCinq = await client.GetFromJsonAsync<JsonElement>("/classements/5v5", Ct);
        Assert.Equal(Photo, Ligne(cinqContreCinq.GetProperty("joueurs"), lie, "playerId").GetProperty("avatarUrl").GetString());
        Assert.Equal(JsonValueKind.Null, Ligne(cinqContreCinq.GetProperty("joueurs"), libre, "playerId").GetProperty("avatarUrl").ValueKind);
    }

    [Fact]
    public async Task La_fiche_expose_la_banniere_du_compte_principal_et_aucune_autre()
    {
        var principal = Factory.CreateAnonymousClient();
        await TestSession.CompleteLoginAsync(principal, Factory.Discord.AddProfile("6101", "ana", null, "ban1", 0x112233, TestSession.GuildId));
        var joueur = await CreatePlayerAsync(principal, "Banniere");
        await ReadJsonAsync(await principal.PostAsJsonAsync("/players/link", new { playerId = joueur }, Ct));
        var second = Factory.CreateAnonymousClient();
        await TestSession.CompleteLoginAsync(second, Factory.Discord.AddProfile("6102", "bob", null, "ban2", null, TestSession.GuildId));
        await ReadJsonAsync(await second.PostAsJsonAsync("/players/link", new { playerId = joueur }, Ct));

        var fiche = await principal.GetFromJsonAsync<JsonElement>($"/players/{joueur}/fiche", Ct);

        var discord = fiche.GetProperty("discord");
        Assert.Equal("https://cdn.discordapp.com/banners/6101/ban1.png", discord.GetProperty("bannerUrl").GetString());
        Assert.Equal("#112233", discord.GetProperty("bannerColor").GetString());
        foreach (var compte in fiche.GetProperty("comptesDiscord").EnumerateArray())
            Assert.Equal(JsonValueKind.Null, compte.GetProperty("bannerUrl").ValueKind);
    }
}
