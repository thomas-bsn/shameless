using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// La liste de tous les Clips (#87), pour l'onglet Clips : paginée, filtrable par auteur, soirée et
/// absence de Game, avec la Game du clip et son score. Elle exige une session.
/// </summary>
public class ClipsListeTests(ApiFactory factory) : IntegrationTest(factory)
{
    // Samedi 12 septembre 2026 : 18 h UTC = 20 h à Paris.
    private static readonly DateTimeOffset Soir = new(2026, 9, 12, 18, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Video = [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6d, 0x70, 0x34, 0x32];

    private async Task<int> DeposerAsync(string attachmentId, DateTimeOffset publieLe, string auteur = "Sam")
    {
        var fichier = new ByteArrayContent(Video);
        fichier.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        var clip = new MultipartFormDataContent
        {
            { new StringContent("1300000000000000001"), "messageId" },
            { new StringContent(attachmentId), "attachmentId" },
            { new StringContent("111111111111111111"), "auteurDiscordId" },
            { new StringContent(auteur), "auteurNom" },
            { new StringContent(publieLe.ToString("O")), "publieLe" },
            { fichier, "fichier", "pentakill.mp4" }
        };
        var reponse = await ReadJsonAsync(await Factory.CreateBotClient().PostAsync("/bot/clips", clip, Ct));
        return reponse.GetProperty("clipId").GetInt32();
    }

    private HttpClient OrganisateurLeSoir()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Clock.SetUtcNow(Soir);
        return client;
    }

    private async Task<JsonElement> ListerAsync(HttpClient client, string requete = "") =>
        await ReadJsonAsync(await client.GetAsync($"/clips{requete}", Ct));

    private static List<int> Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).ToList();

    [Fact]
    public async Task La_liste_exige_une_session()
    {
        var reponse = await Factory.CreateClient().GetAsync("/clips", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
    }

    [Fact]
    public async Task La_liste_expose_l_etat_masque_du_fond_de_chaque_clip()
    {
        var client = OrganisateurLeSoir();
        var visible = await DeposerAsync("1300000000000000401", Soir.AddMinutes(10));
        var masque = await DeposerAsync("1300000000000000402", Soir.AddMinutes(20));
        await using (var db = Factory.CreateDbContext())
            await db.Clips.Where(c => c.Id == masque).ExecuteUpdateAsync(s => s.SetProperty(c => c.MasqueDuFond, true), Ct);

        var page = await ListerAsync(client);

        var etats = page.GetProperty("items").EnumerateArray()
            .ToDictionary(c => c.GetProperty("id").GetInt32(), c => c.GetProperty("masqueDuFond").GetBoolean());
        Assert.False(etats[visible]);
        Assert.True(etats[masque]);
    }

    [Fact]
    public async Task La_liste_rend_tous_les_clips_du_plus_recent_au_plus_ancien_avec_leur_game_et_son_score()
    {
        var client = OrganisateurLeSoir();
        var serie = await CreateSerieAsync(client, format: 3);
        var game1 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        var rattache = await DeposerAsync("1300000000000000301", Soir.AddMinutes(30));
        var sansGame = await DeposerAsync("1300000000000000302", Soir.AddDays(-5));

        var page = await ListerAsync(client);

        Assert.Equal([rattache, sansGame], Ids(page));
        Assert.Equal(2, page.GetProperty("total").GetInt32());
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal("Sam", items[0].GetProperty("auteurNom").GetString());
        Assert.Equal(Video.Length, items[0].GetProperty("taille").GetInt64());
        var game = items[0].GetProperty("game");
        Assert.Equal(game1, game.GetProperty("gameId").GetInt32());
        Assert.Equal(serie.SeriesId, game.GetProperty("seriesId").GetInt32());
        Assert.Equal(1, game.GetProperty("gameNumber").GetInt32());
        Assert.Equal(3, game.GetProperty("bestOf").GetInt32());
        Assert.Equal(1, game.GetProperty("scoreA").GetInt32());
        Assert.Equal(0, game.GetProperty("scoreB").GetInt32());
        Assert.Equal(JsonValueKind.Null, items[1].GetProperty("game").ValueKind);
    }

    [Fact]
    public async Task La_liste_se_filtre_par_auteur_par_soiree_et_par_absence_de_game()
    {
        var client = OrganisateurLeSoir();
        var serie = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        var sam = await DeposerAsync("1300000000000000311", Soir.AddMinutes(30));
        var lea = await DeposerAsync("1300000000000000312", Soir.AddDays(-5), "Léa");

        Assert.Equal([lea], Ids(await ListerAsync(client, "?auteur=léa")));
        Assert.Equal([sam], Ids(await ListerAsync(client, "?soiree=2026-09-12")));
        Assert.Equal([lea], Ids(await ListerAsync(client, "?soiree=2026-09-07")));
        Assert.Equal([lea], Ids(await ListerAsync(client, "?sansGame=true")));
        Assert.Equal([sam], Ids(await ListerAsync(client, "?sansGame=false")));
    }

    [Fact]
    public async Task La_liste_est_paginee()
    {
        var client = OrganisateurLeSoir();
        var ids = new List<int>();
        for (var i = 0; i < 3; i++) ids.Add(await DeposerAsync($"13000000000000003{20 + i}", Soir.AddDays(-i - 1)));

        var page2 = await ListerAsync(client, "?page=2&taille=2");

        Assert.Equal(3, page2.GetProperty("total").GetInt32());
        Assert.Equal(2, page2.GetProperty("page").GetInt32());
        Assert.Equal([ids[2]], Ids(page2));
    }

    [Fact]
    public async Task Une_pagination_ou_une_soiree_invalide_repond_400()
    {
        var client = OrganisateurLeSoir();

        await AssertRefusAsync(await client.GetAsync("/clips?page=0", Ct), HttpStatusCode.BadRequest);
        await AssertRefusAsync(await client.GetAsync("/clips?taille=0", Ct), HttpStatusCode.BadRequest);
        await AssertRefusAsync(await client.GetAsync("/clips?soiree=hier", Ct), HttpStatusCode.BadRequest);
    }
}
