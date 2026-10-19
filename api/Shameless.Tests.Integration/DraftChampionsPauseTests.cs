using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Pause de la Draft de champions : un Capitaine la demande, l'autre l'accepte (le temps restant du tour
/// est gelé à l'acceptation) ou la refuse, sous 60 s ; une pause par Capitaine, 5 minutes au plus ; la
/// reprise anticipée demande aussi l'accord de l'autre. Une seule demande en attente à la fois.
/// </summary>
public class DraftChampionsPauseTests(ApiFactory factory) : IntegrationTest(factory)
{
    private sealed record Preparee(int SeriesId, int GameId, string LienA, string LienB, string LienSpectateur);

    private async Task<Preparee> OuvrirDraftAsync(HttpClient client)
    {
        var serie = await CreateSerieAsync(client);
        await ReadJsonAsync(await client.PutAsJsonAsync($"/series/{serie.SeriesId}/capitaines/A",
            new { designation = "Manuel", playerId = serie.TeamA[0] }, Ct));
        await ReadJsonAsync(await client.PutAsJsonAsync($"/series/{serie.SeriesId}/capitaines/B",
            new { designation = "Manuel", playerId = serie.TeamB[0] }, Ct));
        var gameId = await PlayGameAsync(client, serie.SeriesId, "blue");
        var liens = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}/liens", Ct));
        (await client.PostAsync($"/series/{serie.SeriesId}/draft-champions", null, Ct)).EnsureSuccessStatusCode();
        return new Preparee(serie.SeriesId, gameId,
            liens.GetProperty("lienCapitaineA").GetString()!,
            liens.GetProperty("lienCapitaineB").GetString()!,
            liens.GetProperty("lienSpectateur").GetString()!);
    }

    private Task<HttpResponseMessage> PostAsync(string lien, string chemin, object? corps = null) =>
        Factory.CreateAnonymousClient().PostAsJsonAsync(
            $"/liens/{Uri.EscapeDataString(lien)}/draft-champions/{chemin}", corps ?? new { }, Ct);

    private async Task<JsonElement> LireAsync(string lien) =>
        await ReadJsonAsync(await Factory.CreateAnonymousClient()
            .GetAsync($"/liens/{Uri.EscapeDataString(lien)}/draft-champions", Ct));

    private Task<HttpResponseMessage> JouerAsync(string lien, int tour, string championId) =>
        PostAsync(lien, "actions", new { tour, championId });

    /// <summary>Demande la pause avec <paramref name="demandeur"/> et la fait accepter par <paramref name="autre"/>.</summary>
    private async Task<JsonElement> PauseAccepteeAsync(string demandeur, string autre)
    {
        (await PostAsync(demandeur, "pause")).EnsureSuccessStatusCode();
        return await ReadJsonAsync(await PostAsync(autre, "pause/acceptation"));
    }

    private static DateTime Echeance(JsonElement draft) => draft.GetProperty("tourCourant").GetProperty("echeance").GetDateTime();

    /// <summary>Cale l'horloge à <paramref name="secondes"/> secondes après le début du tour courant.</summary>
    private void CalerApresDebutDuTour(JsonElement draft, int secondes) =>
        Factory.Clock.SetUtcNow(new DateTimeOffset(Echeance(draft), TimeSpan.Zero).AddSeconds(secondes - 60));

    [Fact]
    public async Task La_pause_gele_le_temps_restant_du_tour_et_la_reprise_le_retablit()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await OuvrirDraftAsync(client);
        CalerApresDebutDuTour(await LireAsync(preparee.LienA), 20);

        var pause = await PauseAccepteeAsync(preparee.LienA, preparee.LienB);
        Assert.Equal("A", pause.GetProperty("pause").GetProperty("parEquipe").GetString());
        Assert.Equal(40_000, pause.GetProperty("pause").GetProperty("tempsRestantTourMs").GetInt32());

        Factory.Clock.Advance(TimeSpan.FromMinutes(2));
        var enPause = await LireAsync(preparee.LienSpectateur);
        Assert.Equal(40_000, enPause.GetProperty("pause").GetProperty("tempsRestantTourMs").GetInt32());

        (await PostAsync(preparee.LienA, "reprise")).EnsureSuccessStatusCode();
        var repris = await ReadJsonAsync(await PostAsync(preparee.LienB, "reprise/acceptation"));

        Assert.Equal(JsonValueKind.Null, repris.GetProperty("pause").ValueKind);
        Assert.Equal(Factory.Clock.GetUtcNow().UtcDateTime.AddSeconds(40), Echeance(repris));
    }

    [Fact]
    public async Task Une_seconde_pause_du_meme_capitaine_est_refusee()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await OuvrirDraftAsync(client);
        await PauseAccepteeAsync(preparee.LienA, preparee.LienB);
        (await PostAsync(preparee.LienA, "reprise")).EnsureSuccessStatusCode();
        (await PostAsync(preparee.LienB, "reprise/acceptation")).EnsureSuccessStatusCode();

        await AssertRefusAsync(await PostAsync(preparee.LienA, "pause"), HttpStatusCode.Conflict);
        await PauseAccepteeAsync(preparee.LienB, preparee.LienA);
    }

    [Fact]
    public async Task La_draft_reprend_toute_seule_apres_cinq_minutes_avec_le_temps_restant()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await OuvrirDraftAsync(client);
        var debutTour = Echeance(await LireAsync(preparee.LienA)).AddSeconds(-60);
        CalerApresDebutDuTour(await LireAsync(preparee.LienA), 15);
        await PauseAccepteeAsync(preparee.LienA, preparee.LienB);

        Factory.Clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        Assert.Equal(JsonValueKind.Object, (await LireAsync(preparee.LienB)).GetProperty("pause").ValueKind);

        Factory.Clock.Advance(TimeSpan.FromSeconds(2));
        var repris = await LireAsync(preparee.LienB);

        Assert.Equal(JsonValueKind.Null, repris.GetProperty("pause").ValueKind);
        // 45 s restaient à la pause ; la reprise a lieu à la fin des 5 minutes.
        Assert.Equal(debutTour.AddSeconds(15 + 300 + 45), Echeance(repris));
    }

    [Fact]
    public async Task La_reprise_anticipee_demande_l_accord_de_l_autre_capitaine()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await OuvrirDraftAsync(client);
        await PauseAccepteeAsync(preparee.LienA, preparee.LienB);

        await AssertRefusAsync(await PostAsync(preparee.LienB, "reprise/acceptation"), HttpStatusCode.Conflict);

        var demande = await ReadJsonAsync(await PostAsync(preparee.LienA, "reprise"));
        Assert.Equal("A", demande.GetProperty("repriseDemandee").GetProperty("parEquipe").GetString());
        await AssertRefusAsync(await PostAsync(preparee.LienA, "reprise/acceptation"), HttpStatusCode.Conflict);

        var refus = await ReadJsonAsync(await PostAsync(preparee.LienB, "reprise/refus"));
        Assert.Equal(JsonValueKind.Null, refus.GetProperty("repriseDemandee").ValueKind);
        Assert.Equal(JsonValueKind.Object, refus.GetProperty("pause").ValueKind);
    }

    [Fact]
    public async Task Une_demande_de_reprise_sans_reponse_tombe_au_bout_de_soixante_secondes()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await OuvrirDraftAsync(client);
        await PauseAccepteeAsync(preparee.LienA, preparee.LienB);
        (await PostAsync(preparee.LienA, "reprise")).EnsureSuccessStatusCode();

        Factory.Clock.Advance(TimeSpan.FromSeconds(61));

        var etat = await LireAsync(preparee.LienSpectateur);
        Assert.Equal(JsonValueKind.Null, etat.GetProperty("repriseDemandee").ValueKind);
        Assert.Equal(JsonValueKind.Object, etat.GetProperty("pause").ValueKind);
        await AssertRefusAsync(await PostAsync(preparee.LienB, "reprise/acceptation"), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Pendant_la_pause_personne_ne_joue_et_le_spectateur_ne_met_pas_en_pause()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await OuvrirDraftAsync(client);

        await AssertRefusAsync(await PostAsync(preparee.LienSpectateur, "pause"), HttpStatusCode.Forbidden);
        await PauseAccepteeAsync(preparee.LienB, preparee.LienA);

        await AssertRefusAsync(await JouerAsync(preparee.LienA, 1, "Ahri"), HttpStatusCode.Conflict);
        Factory.Clock.Advance(TimeSpan.FromSeconds(120));
        await AssertRefusAsync(
            await client.PostAsJsonAsync($"/games/{preparee.GameId}/draft-champions/actions", new { tour = 1, championId = "Ahri" }, Ct),
            HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Demander_la_pause_ne_met_pas_en_pause_et_le_temps_est_gele_a_l_acceptation()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await OuvrirDraftAsync(client);
        CalerApresDebutDuTour(await LireAsync(preparee.LienA), 10);

        var demande = await ReadJsonAsync(await PostAsync(preparee.LienA, "pause"));
        Assert.Equal(JsonValueKind.Null, demande.GetProperty("pause").ValueKind);
        Assert.Equal("A", demande.GetProperty("pauseDemandee").GetProperty("parEquipe").GetString());
        Assert.Equal(Factory.Clock.GetUtcNow().UtcDateTime.AddSeconds(60),
            demande.GetProperty("pauseDemandee").GetProperty("expireLe").GetDateTime());
        var vueSpectateur = await LireAsync(preparee.LienSpectateur);
        Assert.Equal("A", vueSpectateur.GetProperty("pauseDemandee").GetProperty("parEquipe").GetString());

        Factory.Clock.Advance(TimeSpan.FromSeconds(30));
        var pause = await ReadJsonAsync(await PostAsync(preparee.LienB, "pause/acceptation"));

        Assert.Equal(JsonValueKind.Null, pause.GetProperty("pauseDemandee").ValueKind);
        Assert.Equal("A", pause.GetProperty("pause").GetProperty("parEquipe").GetString());
        // 10 s écoulées + 30 s d'attente : il restait 20 s à l'acceptation.
        Assert.Equal(20_000, pause.GetProperty("pause").GetProperty("tempsRestantTourMs").GetInt32());
    }

    [Fact]
    public async Task Le_demandeur_ne_peut_pas_accepter_ni_refuser_sa_propre_demande_ni_sans_demande()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await OuvrirDraftAsync(client);

        await AssertRefusAsync(await PostAsync(preparee.LienB, "pause/acceptation"), HttpStatusCode.Conflict);
        await AssertRefusAsync(await PostAsync(preparee.LienB, "pause/refus"), HttpStatusCode.Conflict);

        (await PostAsync(preparee.LienA, "pause")).EnsureSuccessStatusCode();
        await AssertRefusAsync(await PostAsync(preparee.LienA, "pause/acceptation"), HttpStatusCode.Conflict);
        await AssertRefusAsync(await PostAsync(preparee.LienA, "pause/refus"), HttpStatusCode.Conflict);
        await AssertRefusAsync(await PostAsync(preparee.LienSpectateur, "pause/acceptation"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Une_pause_refusee_ne_consomme_pas_la_pause_du_demandeur()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await OuvrirDraftAsync(client);
        (await PostAsync(preparee.LienA, "pause")).EnsureSuccessStatusCode();

        var refus = await ReadJsonAsync(await PostAsync(preparee.LienB, "pause/refus"));

        Assert.Equal(JsonValueKind.Null, refus.GetProperty("pauseDemandee").ValueKind);
        Assert.Equal(JsonValueKind.Null, refus.GetProperty("pause").ValueKind);
        Assert.True(refus.GetProperty("pauseDisponibleA").GetBoolean());
        var pause = await PauseAccepteeAsync(preparee.LienA, preparee.LienB);
        Assert.Equal("A", pause.GetProperty("pause").GetProperty("parEquipe").GetString());
    }

    [Fact]
    public async Task Une_demande_de_pause_sans_reponse_tombe_apres_soixante_secondes_sans_consommer_la_pause()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await OuvrirDraftAsync(client);
        (await PostAsync(preparee.LienA, "pause")).EnsureSuccessStatusCode();

        Factory.Clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(JsonValueKind.Object, (await LireAsync(preparee.LienB)).GetProperty("pauseDemandee").ValueKind);

        Factory.Clock.Advance(TimeSpan.FromSeconds(2));
        var etat = await LireAsync(preparee.LienSpectateur);
        Assert.Equal(JsonValueKind.Null, etat.GetProperty("pauseDemandee").ValueKind);
        Assert.Equal(JsonValueKind.Null, etat.GetProperty("pause").ValueKind);
        Assert.True(etat.GetProperty("pauseDisponibleA").GetBoolean());
        await AssertRefusAsync(await PostAsync(preparee.LienB, "pause/acceptation"), HttpStatusCode.Conflict);
        (await PostAsync(preparee.LienA, "pause")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Une_seule_demande_a_la_fois_pause_reprise_et_reset_se_refusent_entre_elles_en_409()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await OuvrirDraftAsync(client);

        (await PostAsync(preparee.LienA, "pause")).EnsureSuccessStatusCode();
        await AssertRefusAsync(await PostAsync(preparee.LienB, "pause"), HttpStatusCode.Conflict);
        await AssertRefusAsync(await PostAsync(preparee.LienB, "reset"), HttpStatusCode.Conflict);
        (await PostAsync(preparee.LienB, "pause/refus")).EnsureSuccessStatusCode();

        (await PostAsync(preparee.LienA, "reset")).EnsureSuccessStatusCode();
        await AssertRefusAsync(await PostAsync(preparee.LienB, "pause"), HttpStatusCode.Conflict);
        (await PostAsync(preparee.LienB, "reset/reponse", new { accepte = false })).EnsureSuccessStatusCode();

        await PauseAccepteeAsync(preparee.LienA, preparee.LienB);
        (await PostAsync(preparee.LienA, "reprise")).EnsureSuccessStatusCode();
        await AssertRefusAsync(await PostAsync(preparee.LienB, "reset"), HttpStatusCode.Conflict);
        await AssertRefusAsync(await PostAsync(preparee.LienB, "pause"), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Un_conflit_de_demande_donne_un_message_lisible()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await OuvrirDraftAsync(client);
        (await PostAsync(preparee.LienA, "pause")).EnsureSuccessStatusCode();

        var reponse = await PostAsync(preparee.LienB, "reset");

        Assert.Equal(HttpStatusCode.Conflict, reponse.StatusCode);
        Assert.Contains("demande", await reponse.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task La_pause_est_publiee_dans_openapi()
    {
        var client = Factory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();
        var document = await ReadJsonAsync(await client.GetAsync("/openapi/v1.json", Ct));
        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/liens/{lien}/draft-champions/pause", out _));
        Assert.True(paths.TryGetProperty("/liens/{lien}/draft-champions/pause/acceptation", out _));
        Assert.True(paths.TryGetProperty("/liens/{lien}/draft-champions/pause/refus", out _));
        Assert.True(paths.TryGetProperty("/liens/{lien}/draft-champions/reprise", out _));
        Assert.True(paths.TryGetProperty("/liens/{lien}/draft-champions/reprise/acceptation", out _));
        Assert.True(paths.TryGetProperty("/liens/{lien}/draft-champions/reprise/refus", out _));
    }
}
