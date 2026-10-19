using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Pause de la Draft de champions : un Capitaine la met en pause tout de suite (le temps restant du tour
/// est gelé), une fois par Capitaine, 5 minutes au plus ; la reprise anticipée demande l'accord de l'autre.
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

        var pause = await ReadJsonAsync(await PostAsync(preparee.LienA, "pause"));
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
        (await PostAsync(preparee.LienA, "pause")).EnsureSuccessStatusCode();
        (await PostAsync(preparee.LienA, "reprise")).EnsureSuccessStatusCode();
        (await PostAsync(preparee.LienB, "reprise/acceptation")).EnsureSuccessStatusCode();

        await AssertRefusAsync(await PostAsync(preparee.LienA, "pause"), HttpStatusCode.Conflict);
        (await PostAsync(preparee.LienB, "pause")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task La_draft_reprend_toute_seule_apres_cinq_minutes_avec_le_temps_restant()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await OuvrirDraftAsync(client);
        var debutTour = Echeance(await LireAsync(preparee.LienA)).AddSeconds(-60);
        CalerApresDebutDuTour(await LireAsync(preparee.LienA), 15);
        (await PostAsync(preparee.LienA, "pause")).EnsureSuccessStatusCode();

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
        (await PostAsync(preparee.LienA, "pause")).EnsureSuccessStatusCode();

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
        (await PostAsync(preparee.LienA, "pause")).EnsureSuccessStatusCode();
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
        (await PostAsync(preparee.LienB, "pause")).EnsureSuccessStatusCode();

        await AssertRefusAsync(await JouerAsync(preparee.LienA, 1, "Ahri"), HttpStatusCode.Conflict);
        Factory.Clock.Advance(TimeSpan.FromSeconds(120));
        await AssertRefusAsync(
            await client.PostAsJsonAsync($"/games/{preparee.GameId}/draft-champions/actions", new { tour = 1, championId = "Ahri" }, Ct),
            HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task La_pause_est_publiee_dans_openapi()
    {
        var client = Factory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();
        var document = await ReadJsonAsync(await client.GetAsync("/openapi/v1.json", Ct));
        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/liens/{lien}/draft-champions/pause", out _));
        Assert.True(paths.TryGetProperty("/liens/{lien}/draft-champions/reprise", out _));
        Assert.True(paths.TryGetProperty("/liens/{lien}/draft-champions/reprise/acceptation", out _));
        Assert.True(paths.TryGetProperty("/liens/{lien}/draft-champions/reprise/refus", out _));
    }
}
