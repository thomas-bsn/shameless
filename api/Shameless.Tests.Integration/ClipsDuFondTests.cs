using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Les Clips du Fond (spec #104) : la liste des Clips éligibles, tirée au hasard côté serveur, sans les
/// Clips masqués du Fond, et qui exige une session.
/// </summary>
public class ClipsDuFondTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static readonly DateTimeOffset Soir = new(2026, 9, 12, 18, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Video = [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6d, 0x70, 0x34, 0x32];

    private async Task<int> DeposerAsync(string attachmentId)
    {
        var fichier = new ByteArrayContent(Video);
        fichier.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        var clip = new MultipartFormDataContent
        {
            { new StringContent("1300000000000000001"), "messageId" },
            { new StringContent(attachmentId), "attachmentId" },
            { new StringContent("111111111111111111"), "auteurDiscordId" },
            { new StringContent("Mina"), "auteurNom" },
            { new StringContent(Soir.ToString("O")), "publieLe" },
            { fichier, "fichier", "pentakill.mp4" }
        };
        return (await ReadJsonAsync(await Factory.CreateBotClient().PostAsync("/bot/clips", clip, Ct)))
            .GetProperty("clipId").GetInt32();
    }

    private async Task MasquerAsync(int clipId)
    {
        await using var db = Factory.CreateDbContext();
        await db.Clips.Where(c => c.Id == clipId).ExecuteUpdateAsync(s => s.SetProperty(c => c.MasqueDuFond, true), Ct);
    }

    private async Task<List<JsonElement>> FondAsync(HttpClient client) =>
        (await ReadJsonAsync(await client.GetAsync("/clips/fond", Ct))).EnumerateArray().ToList();

    [Fact]
    public async Task Le_fond_liste_les_clips_non_masques_avec_leur_auteur_et_leur_game()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Clock.SetUtcNow(Soir);
        var visible = await DeposerAsync("1300000000000000301");
        var masque = await DeposerAsync("1300000000000000302");
        await MasquerAsync(masque);

        var fond = await FondAsync(client);

        var clip = Assert.Single(fond);
        Assert.Equal(visible, clip.GetProperty("id").GetInt32());
        Assert.Equal("Mina", clip.GetProperty("auteurNom").GetString());
        Assert.Equal(JsonValueKind.Null, clip.GetProperty("gameId").ValueKind);
    }

    [Fact]
    public async Task Le_fond_est_vide_sans_clip()
    {
        var client = Factory.CreateSignedInClient();

        Assert.Empty(await FondAsync(client));
    }

    [Fact]
    public async Task Le_fond_est_tire_au_hasard_cote_serveur_sans_perdre_de_clip()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Clock.SetUtcNow(Soir);
        var ids = new List<int>();
        for (var i = 0; i < 6; i++) ids.Add(await DeposerAsync($"130000000000000040{i}"));

        var ordres = new HashSet<string>();
        for (var i = 0; i < 12; i++)
        {
            var fond = (await FondAsync(client)).Select(c => c.GetProperty("id").GetInt32()).ToList();
            Assert.Equal(ids.Order(), fond.Order());
            ordres.Add(string.Join(',', fond));
        }

        Assert.True(ordres.Count > 1, "L'ordre du Fond ne change jamais.");
    }

    [Fact]
    public async Task Le_fond_exige_une_session()
    {
        var reponse = await Factory.CreateClient().GetAsync("/clips/fond", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
    }

    [Fact]
    public async Task Masquer_et_reafficher_exigent_une_session()
    {
        var anonyme = Factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonyme.PutAsync("/clips/1/masque-du-fond", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonyme.DeleteAsync("/clips/1/masque-du-fond", Ct)).StatusCode);
    }

    [Fact]
    public async Task Masquer_et_reafficher_sont_reserves_aux_administrateurs()
    {
        var joueur = Factory.CreateSignedInClient();
        Factory.Clock.SetUtcNow(Soir);
        var clip = await DeposerAsync("1300000000000000501");

        Assert.Equal(HttpStatusCode.Forbidden, (await joueur.PutAsync($"/clips/{clip}/masque-du-fond", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await joueur.DeleteAsync($"/clips/{clip}/masque-du-fond", Ct)).StatusCode);
        Assert.Single(await FondAsync(joueur));
    }

    [Fact]
    public async Task Un_clip_masque_sort_du_fond_mais_reste_dans_la_liste_puis_revient_une_fois_reaffiche()
    {
        var admin = Factory.CreateAdminClient();
        Factory.Clock.SetUtcNow(Soir);
        var clip = await DeposerAsync("1300000000000000502");

        var masque = await admin.PutAsync($"/clips/{clip}/masque-du-fond", null, Ct);
        Assert.Equal(HttpStatusCode.OK, masque.StatusCode);
        Assert.True((await ReadJsonAsync(masque)).GetProperty("masqueDuFond").GetBoolean());
        Assert.Empty(await FondAsync(admin));
        var liste = (await ReadJsonAsync(await admin.GetAsync("/clips", Ct))).GetProperty("items").EnumerateArray().Single();
        Assert.True(liste.GetProperty("masqueDuFond").GetBoolean());

        var reaffiche = await admin.DeleteAsync($"/clips/{clip}/masque-du-fond", Ct);
        Assert.Equal(HttpStatusCode.OK, reaffiche.StatusCode);
        Assert.False((await ReadJsonAsync(reaffiche)).GetProperty("masqueDuFond").GetBoolean());
        Assert.Single(await FondAsync(admin));
    }

    [Fact]
    public async Task Masquer_un_clip_inconnu_repond_404()
    {
        var admin = Factory.CreateAdminClient();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync("/clips/9999/masque-du-fond", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync("/clips/9999/masque-du-fond", Ct)).StatusCode);
    }
}
