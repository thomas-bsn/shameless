using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Shameless.Infrastructure.Data;
using Shameless.Infrastructure.Data.Repositories;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Les Clips de <c>#clips</c> (ADR 0014) : le bot dépose la vidéo téléchargée (<c>POST /bot/clips</c>),
/// l'API la conserve dans le stockage de l'Instance et la rattache à sa Game seulement si une unique
/// Game plausible existe le jour de publication (fuseau de l'Instance : Europe/Paris en test). Le site
/// lit les clips d'une Game et leur vidéo.
/// </summary>
public class ClipsTests(ApiFactory factory) : IntegrationTest(factory)
{
    // Samedi 12 septembre 2026 : 18 h UTC = 20 h à Paris.
    private static readonly DateTimeOffset Soir = new(2026, 9, 12, 18, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Video = [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6d, 0x70, 0x34, 0x32];

    private static MultipartFormDataContent Clip(
        string attachmentId,
        DateTimeOffset publieLe,
        byte[]? video = null,
        string contentType = "video/mp4",
        string nomFichier = "pentakill.mp4",
        string messageId = "1300000000000000001")
    {
        var fichier = new ByteArrayContent(video ?? Video);
        fichier.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent
        {
            { new StringContent(messageId), "messageId" },
            { new StringContent(attachmentId), "attachmentId" },
            { new StringContent("111111111111111111"), "auteurDiscordId" },
            { new StringContent("Sam"), "auteurNom" },
            { new StringContent(publieLe.ToString("O")), "publieLe" },
            { fichier, "fichier", nomFichier }
        };
    }

    private async Task<HttpResponseMessage> DeposerAsync(MultipartFormDataContent clip) =>
        await Factory.CreateBotClient().PostAsync("/bot/clips", clip, Ct);

    /// <summary>Connecte l'organisateur puis règle l'horloge sur <see cref="Soir"/> (le cookie reste valide).</summary>
    private HttpClient OrganisateurLeSoir()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Clock.SetUtcNow(Soir);
        return client;
    }

    [Fact]
    public async Task Liste_et_Fond_excluent_les_clips_masques_et_restreignent_les_actions_aux_admins_connectes()
    {
        var admin = Factory.CreateAdminClient();
        var client = OrganisateurLeSoir();
        var depose = await ReadJsonAsync(await DeposerAsync(Clip("1300000000000000199", Soir.AddMinutes(40))));
        var clipId = depose.GetProperty("clipId").GetInt32();

        var dansLeFond = await ReadJsonAsync(await client.GetAsync("/clips/fond", Ct));
        Assert.Contains(dansLeFond.EnumerateArray(), c => c.GetProperty("id").GetInt32() == clipId);

        var refus = await client.PutAsJsonAsync($"/clips/{clipId}/fond", new { masque = true }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, refus.StatusCode);
        var profilAdmin = await admin.GetFromJsonAsync<JsonElement>("/auth/me", Ct);
        Assert.True(profilAdmin.GetProperty("isAdmin").GetBoolean());
        var masque = await admin.PutAsJsonAsync($"/clips/{clipId}/fond", new { masque = true }, Ct);
        Assert.True(masque.StatusCode == HttpStatusCode.OK, $"Masquer le clip a renvoyé {(int)masque.StatusCode} : {await masque.Content.ReadAsStringAsync(Ct)}");
        var fondApres = await ReadJsonAsync(await client.GetAsync("/clips/fond", Ct));
        Assert.DoesNotContain(fondApres.EnumerateArray(), c => c.GetProperty("id").GetInt32() == clipId);
        var fil = await ReadJsonAsync(await client.GetAsync("/clips", Ct));
        Assert.Contains(fil.EnumerateArray(), c => c.GetProperty("id").GetInt32() == clipId && c.GetProperty("masqueDuFond").GetBoolean());

        var anonyme = Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonyme.GetAsync("/clips/fond", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonyme.PutAsJsonAsync($"/clips/{clipId}/fond", new { masque = false }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Un_clip_publie_le_jour_d_une_unique_game_lui_est_rattache_et_se_lit_depuis_la_game()
    {
        var client = OrganisateurLeSoir();
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");

        var response = await DeposerAsync(Clip("1300000000000000101", Soir.AddMinutes(50)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var depose = await ReadJsonAsync(response);
        Assert.Equal(gameId, depose.GetProperty("gameId").GetInt32());
        Assert.Equal("automatique", depose.GetProperty("rattachement").GetString());

        var clips = (await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/clips", Ct))).EnumerateArray().ToList();
        var clip = Assert.Single(clips);
        Assert.Equal(depose.GetProperty("clipId").GetInt32(), clip.GetProperty("id").GetInt32());
        Assert.Equal("Sam", clip.GetProperty("auteurNom").GetString());
        Assert.Equal("111111111111111111", clip.GetProperty("auteurDiscordId").GetString());
        Assert.Equal("video/mp4", clip.GetProperty("contentType").GetString());
        Assert.Equal(Video.Length, clip.GetProperty("taille").GetInt64());
        Assert.Equal(Soir.AddMinutes(50), clip.GetProperty("publieLe").GetDateTimeOffset());

        var fichier = await client.GetAsync($"/clips/{clip.GetProperty("id").GetInt32()}/fichier", Ct);
        Assert.Equal(HttpStatusCode.OK, fichier.StatusCode);
        Assert.Equal("video/mp4", fichier.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Video, await fichier.Content.ReadAsByteArrayAsync(Ct));
    }

    [Fact]
    public async Task Un_clip_publie_un_jour_a_deux_games_reste_sans_game_mais_conserve()
    {
        var client = OrganisateurLeSoir();
        var serie = await CreateSerieAsync(client, format: 3);
        var game1 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        Factory.Clock.Advance(TimeSpan.FromMinutes(40));
        var game2 = await PlayAndWinAsync(client, serie.SeriesId, "B");

        var depose = await ReadJsonAsync(await DeposerAsync(Clip("1300000000000000102", Soir.AddHours(2))));

        Assert.Equal(JsonValueKind.Null, depose.GetProperty("gameId").ValueKind);
        Assert.Equal(JsonValueKind.Null, depose.GetProperty("rattachement").ValueKind);
        Assert.Equal(0, (await ReadJsonAsync(await client.GetAsync($"/games/{game1}/clips", Ct))).GetArrayLength());
        Assert.Equal(0, (await ReadJsonAsync(await client.GetAsync($"/games/{game2}/clips", Ct))).GetArrayLength());
        var fichier = await client.GetAsync($"/clips/{depose.GetProperty("clipId").GetInt32()}/fichier", Ct);
        Assert.Equal(Video, await fichier.Content.ReadAsByteArrayAsync(Ct));
    }

    [Fact]
    public async Task Un_clip_publie_entre_deux_games_va_a_la_seule_deja_lancee()
    {
        var client = OrganisateurLeSoir();
        var serie = await CreateSerieAsync(client, format: 3);
        var game1 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        Factory.Clock.Advance(TimeSpan.FromMinutes(40));
        await PlayAndWinAsync(client, serie.SeriesId, "B");

        var depose = await ReadJsonAsync(await DeposerAsync(Clip("1300000000000000103", Soir.AddMinutes(30))));

        Assert.Equal(game1, depose.GetProperty("gameId").GetInt32());
    }

    [Fact]
    public async Task Un_clip_publie_apres_minuit_heure_de_l_instance_ne_va_pas_a_la_game_de_la_veille()
    {
        var client = OrganisateurLeSoir();
        var serie = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, serie.SeriesId, "A");

        // 22 h 30 UTC = 0 h 30 à Paris, le lendemain de la game (lancée à 20 h à Paris).
        var depose = await ReadJsonAsync(await DeposerAsync(Clip("1300000000000000104", Soir.AddHours(4.5))));

        Assert.Equal(JsonValueKind.Null, depose.GetProperty("gameId").ValueKind);
    }

    [Fact]
    public async Task Un_clip_sans_game_ce_jour_la_reste_sans_game()
    {
        OrganisateurLeSoir();

        var response = await DeposerAsync(Clip("1300000000000000105", Soir));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ReadJsonAsync(response)).GetProperty("gameId").ValueKind);
    }

    [Fact]
    public async Task Le_meme_depot_renvoye_rend_le_clip_deja_conserve_sans_doublon()
    {
        var client = OrganisateurLeSoir();
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");
        var premier = await ReadJsonAsync(await DeposerAsync(Clip("1300000000000000106", Soir.AddMinutes(50))));

        var renvoi = await DeposerAsync(Clip("1300000000000000106", Soir.AddMinutes(50), video: [1, 2, 3]));

        Assert.Equal(HttpStatusCode.OK, renvoi.StatusCode);
        Assert.Equal(premier.GetProperty("clipId").GetInt32(), (await ReadJsonAsync(renvoi)).GetProperty("clipId").GetInt32());
        var clip = Assert.Single((await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/clips", Ct))).EnumerateArray());
        var fichier = await client.GetAsync($"/clips/{clip.GetProperty("id").GetInt32()}/fichier", Ct);
        Assert.Equal(Video, await fichier.Content.ReadAsByteArrayAsync(Ct));
        Assert.Single(Directory.GetFiles(Factory.DossierDesClips));
    }

    /// <summary>Le dépôt des clips réel, dont l'enregistrement échoue sur une erreur de la base.</summary>
    private sealed class DepotDesClipsEnPanne(AppDbContext db) : IClipRepository
    {
        private readonly ClipRepository _reel = new(db);
        public void Add(Clip clip) => _reel.Add(clip);
        public Task<Clip?> GetAsync(int id) => _reel.GetAsync(id);
        public Task<Clip?> GetByAttachmentIdAsync(long discordAttachmentId) => _reel.GetByAttachmentIdAsync(discordAttachmentId);
        public Task<List<Clip>> GetDeLaGameAsync(int gameId) => _reel.GetDeLaGameAsync(gameId);
        public Task<List<Clip>> GetSansGameAsync() => _reel.GetSansGameAsync();
        public Task<List<Clip>> GetTousAsync(bool? sansGame, bool? masques, string? auteur, int? soireeId, int page, int pageSize) => _reel.GetTousAsync(sansGame, masques, auteur, soireeId, page, pageSize);
        public Task<List<Clip>> GetEligiblesAuFondAsync(int take) => _reel.GetEligiblesAuFondAsync(take);
        public Task<bool> TrySaveChangesAsync() => throw new InvalidOperationException("La base est en panne.");
    }

    [Fact]
    public async Task Si_le_clip_ne_peut_pas_etre_enregistre_sa_video_n_est_pas_gardee()
    {
        OrganisateurLeSoir();
        using var enPanne = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<IClipRepository>();
            services.AddScoped<IClipRepository, DepotDesClipsEnPanne>();
        }));
        var bot = enPanne.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        bot.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestSession.BotKey);

        try
        {
            var response = await bot.PostAsync("/bot/clips", Clip("1300000000000000109", Soir), Ct);
            Assert.False(response.IsSuccessStatusCode);
        }
        catch (InvalidOperationException)
        {
            // L'erreur peut remonter telle quelle depuis le serveur de test.
        }

        Assert.False(Directory.Exists(Factory.DossierDesClips) && Directory.GetFiles(Factory.DossierDesClips).Length > 0);
    }

    [Fact]
    public async Task La_video_d_un_clip_se_lit_par_plages()
    {
        var client = OrganisateurLeSoir();
        var clipId = (await ReadJsonAsync(await DeposerAsync(Clip("1300000000000000107", Soir)))).GetProperty("clipId").GetInt32();

        var requete = new HttpRequestMessage(HttpMethod.Get, $"/clips/{clipId}/fichier");
        requete.Headers.Range = new RangeHeaderValue(4, 7);
        var plage = await client.SendAsync(requete, Ct);

        Assert.Equal(HttpStatusCode.PartialContent, plage.StatusCode);
        Assert.Equal(Video[4..8], await plage.Content.ReadAsByteArrayAsync(Ct));
    }

    [Fact]
    public async Task Un_fichier_qui_n_est_pas_une_video_est_refuse()
    {
        OrganisateurLeSoir();

        var response = await DeposerAsync(Clip("1300000000000000108", Soir, contentType: "image/png", nomFichier: "capture.png"));

        await AssertRefusAsync(response, HttpStatusCode.BadRequest);
        Assert.False(Directory.Exists(Factory.DossierDesClips) && Directory.GetFiles(Factory.DossierDesClips).Length > 0);
    }

    [Fact]
    public async Task Un_depot_sans_identifiants_discord_valides_est_refuse()
    {
        OrganisateurLeSoir();

        await AssertRefusAsync(await DeposerAsync(Clip("pas-un-snowflake", Soir)), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Une_video_plus_grande_que_la_taille_maximale_est_refusee()
    {
        OrganisateurLeSoir();

        var response = await DeposerAsync(Clip("1300000000000000109", Soir, video: new byte[ApiFactory.TailleMaxDesClips + 1]));

        await AssertRefusAsync(response, HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Un_clip_dont_la_game_en_attente_disparait_reste_sans_game()
    {
        var client = OrganisateurLeSoir();
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayGameAsync(client, serie.SeriesId);
        var depose = await ReadJsonAsync(await DeposerAsync(Clip("1300000000000000110", Soir.AddMinutes(10))));
        Assert.Equal(gameId, depose.GetProperty("gameId").GetInt32());
        var clipId = depose.GetProperty("clipId").GetInt32();

        // Série annulée : sa game en attente est effacée (ADR 0009).
        (await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct)).EnsureSuccessStatusCode();

        var fichier = await client.GetAsync($"/clips/{clipId}/fichier", Ct);
        Assert.Equal(HttpStatusCode.OK, fichier.StatusCode);
    }

    [Fact]
    public async Task Les_clips_d_une_game_inconnue_et_la_video_d_un_clip_inconnu_repondent_404()
    {
        var client = Factory.CreateSignedInClient();

        await AssertRefusAsync(await client.GetAsync("/games/999/clips", Ct), HttpStatusCode.NotFound);
        await AssertRefusAsync(await client.GetAsync("/clips/999/fichier", Ct), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Une_session_d_utilisateur_ne_depose_pas_de_clip()
    {
        var client = OrganisateurLeSoir();

        await AssertRefusAsync(await client.PostAsync("/bot/clips", Clip("1300000000000000111", Soir), Ct), HttpStatusCode.Unauthorized);
    }
}
