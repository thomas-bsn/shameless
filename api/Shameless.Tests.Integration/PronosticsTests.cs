using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Les pronostics des Spectateurs : déposés par le bot avant le lancement de la prochaine Game d'une
/// Série (vainqueur, DOFD), verrouillés à son lancement, puis évalués séparément quand chaque
/// résultat est disponible — sans points ni classement.
/// </summary>
public class PronosticsTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const string DiscordA1 = "100000000000000001";
    private const string Spectateur = "900000000000000001";
    private const string Spectateur2 = "900000000000000002";

    private Task<HttpResponseMessage> PronostiquerAsync(
        int seriesId, string pronostiqueur, int gameNumber, string? equipeGagnante, int? dofd, string? nom = null) =>
        Factory.CreateBotClient().PutAsJsonAsync(
            $"/bot/series/{seriesId}/pronostics",
            new
            {
                pronostiqueurDiscordId = pronostiqueur,
                pronostiqueurNom = nom,
                gameNumber,
                equipeGagnante,
                dofdPlayerId = dofd,
            },
            Ct);

    private Task<HttpResponseMessage> VoterAsync(int gameId, string votant, int? dofd) =>
        Factory.CreateBotClient().PutAsJsonAsync(
            $"/bot/games/{gameId}/votes",
            new { votantDiscordId = votant, dofdPlayerId = dofd },
            Ct);

    private static JsonElement PronosticDe(JsonElement pronostics, string discordId) =>
        pronostics.GetProperty("pronostics").EnumerateArray()
            .Single(p => p.GetProperty("pronostiqueurDiscordId").GetString() == discordId);

    private static bool? Correct(JsonElement pronostic, string element) =>
        pronostic.GetProperty(element).GetProperty("correct") is { ValueKind: not JsonValueKind.Null } correct
            ? correct.GetBoolean()
            : null;

    [Fact]
    public async Task Un_spectateur_pronostique_avant_la_game_et_voit_ses_choix_corrects_apres()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await LierADiscordAsync(serie.TeamA[0], DiscordA1);

        var depot = await ReadJsonAsync(await PronostiquerAsync(
            serie.SeriesId, Spectateur, gameNumber: 1, equipeGagnante: "A", dofd: serie.TeamB[3], nom: "Kiki"));
        Assert.Equal(1, depot.GetProperty("gameNumber").GetInt32());

        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");
        await ReadJsonAsync(await VoterAsync(gameId, DiscordA1, dofd: serie.TeamB[3]));
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        var pronostics = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/pronostics", Ct));
        var pronostic = PronosticDe(pronostics, Spectateur);

        Assert.Equal("Kiki", pronostic.GetProperty("pronostiqueurNom").GetString());
        Assert.Equal("A", pronostic.GetProperty("equipeGagnante").GetProperty("equipe").GetString());
        Assert.True(Correct(pronostic, "equipeGagnante"));
        Assert.Equal("B4", pronostic.GetProperty("dofd").GetProperty("prenom").GetString());
        Assert.True(Correct(pronostic, "dofd"));
    }

    [Fact]
    public async Task Les_pronostics_restent_ouverts_apres_le_lancement_de_la_draft_et_se_closent_au_debut_de_la_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await ReadJsonAsync(await PronostiquerAsync(serie.SeriesId, Spectateur, 1, "A", null));

        var gameId = await PlayGameAsync(client, serie.SeriesId);

        // Draft lancée, partie pas commencée : le pronostic de la game 1 est encore possible.
        var avant = await ReadJsonAsync(await Factory.CreateBotClient().GetAsync($"/bot/series/{serie.SeriesId}/pronostics", Ct));
        Assert.Equal(1, avant.GetProperty("gameNumber").GetInt32());
        await ReadJsonAsync(await PronostiquerAsync(serie.SeriesId, Spectateur2, 1, "B", null));

        await JouerLaDraftAsync(client, serie.SeriesId, gameId);

        await AssertRefusAsync(await PronostiquerAsync(serie.SeriesId, Spectateur, 1, "B", null), HttpStatusCode.Conflict);
        await AssertRefusAsync(await PronostiquerAsync(serie.SeriesId, Spectateur2, 2, "B", null), HttpStatusCode.Conflict);
        var ouverts = await ReadJsonAsync(await Factory.CreateBotClient().GetAsync($"/bot/series/{serie.SeriesId}/pronostics", Ct));
        Assert.Equal(JsonValueKind.Null, ouverts.GetProperty("gameNumber").ValueKind);

        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "B" }, Ct));
        var pronostics = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/pronostics", Ct));
        Assert.Equal("A", PronosticDe(pronostics, Spectateur).GetProperty("equipeGagnante").GetProperty("equipe").GetString());
        Assert.False(Correct(PronosticDe(pronostics, Spectateur), "equipeGagnante"));
    }

    [Fact]
    public async Task Entre_deux_games_on_pronostique_la_suivante()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var game1 = await PlayAndWinAsync(client, serie.SeriesId, "A");

        var ouverts = await ReadJsonAsync(await Factory.CreateBotClient().GetAsync($"/bot/series/{serie.SeriesId}/pronostics", Ct));
        Assert.Equal(2, ouverts.GetProperty("gameNumber").GetInt32());
        Assert.Equal(5, ouverts.GetProperty("teamA").GetArrayLength());
        Assert.Equal("B1", ouverts.GetProperty("teamB")[0].GetProperty("prenom").GetString());

        await AssertRefusAsync(await PronostiquerAsync(serie.SeriesId, Spectateur, 3, "A", null), HttpStatusCode.BadRequest);
        await ReadJsonAsync(await PronostiquerAsync(serie.SeriesId, Spectateur, 2, "B", null));
        var game2 = await PlayAndWinAsync(client, serie.SeriesId, "B");

        var pronostics1 = await ReadJsonAsync(await client.GetAsync($"/games/{game1}/pronostics", Ct));
        Assert.Empty(pronostics1.GetProperty("pronostics").EnumerateArray());
        var pronostics2 = await ReadJsonAsync(await client.GetAsync($"/games/{game2}/pronostics", Ct));
        Assert.Equal(2, pronostics2.GetProperty("gameNumber").GetInt32());
        Assert.True(Correct(PronosticDe(pronostics2, Spectateur), "equipeGagnante"));
    }

    [Fact]
    public async Task Les_joueurs_de_la_serie_ne_pronostiquent_pas()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await LierADiscordAsync(serie.TeamA[0], DiscordA1);

        await AssertRefusAsync(await PronostiquerAsync(serie.SeriesId, DiscordA1, 1, "A", null), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task On_pronostique_une_equipe_et_un_joueur_de_la_serie()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var horsSerie = await CreatePlayerAsync(client, "Z9");

        await AssertRefusAsync(await PronostiquerAsync(serie.SeriesId, Spectateur, 1, "C", null), HttpStatusCode.BadRequest);
        await AssertRefusAsync(await PronostiquerAsync(serie.SeriesId, Spectateur, 1, null, horsSerie), HttpStatusCode.BadRequest);
        await AssertRefusAsync(await PronostiquerAsync(serie.SeriesId, "pas-un-id", 1, "A", null), HttpStatusCode.BadRequest);
        await AssertRefusAsync(await PronostiquerAsync(999, Spectateur, 1, "A", null), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Chaque_choix_est_evalue_des_que_son_resultat_est_disponible()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await LierADiscordAsync(serie.TeamA[0], DiscordA1);
        await ReadJsonAsync(await PronostiquerAsync(serie.SeriesId, Spectateur, 1, "B", serie.TeamA[1]));
        var gameId = await PlayGameAsync(client, serie.SeriesId);

        var enCours = PronosticDe(await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/pronostics", Ct)), Spectateur);
        Assert.Null(Correct(enCours, "equipeGagnante"));
        Assert.Null(Correct(enCours, "dofd"));

        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "A" }, Ct));
        await ReadJsonAsync(await VoterAsync(gameId, DiscordA1, dofd: serie.TeamA[1]));
        var pendantLesVotes = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/pronostics", Ct));
        Assert.True(pendantLesVotes.GetProperty("vainqueurConnu").GetBoolean());
        Assert.False(pendantLesVotes.GetProperty("votesClos").GetBoolean());
        Assert.False(Correct(PronosticDe(pendantLesVotes, Spectateur), "equipeGagnante"));
        Assert.Null(Correct(PronosticDe(pendantLesVotes, Spectateur), "dofd"));

        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        var apres = await ReadJsonAsync(await Factory.CreateBotClient().GetAsync($"/bot/games/{gameId}/pronostics", Ct));
        Assert.True(Correct(PronosticDe(apres, Spectateur), "dofd"));
    }

    [Fact]
    public async Task Un_dofd_a_egalite_rend_juste_le_pronostic_de_chacun_des_gagnants()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await LierADiscordAsync(serie.TeamA[0], DiscordA1);
        await LierADiscordAsync(serie.TeamB[0], "100000000000000006");
        await ReadJsonAsync(await PronostiquerAsync(serie.SeriesId, Spectateur, 1, null, serie.TeamA[2]));
        await ReadJsonAsync(await PronostiquerAsync(serie.SeriesId, Spectateur2, 1, null, serie.TeamB[2]));
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");
        await ReadJsonAsync(await VoterAsync(gameId, DiscordA1, dofd: serie.TeamA[2]));
        await ReadJsonAsync(await VoterAsync(gameId, "100000000000000006", dofd: serie.TeamB[2]));
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        var pronostics = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/pronostics", Ct));

        Assert.True(Correct(PronosticDe(pronostics, Spectateur), "dofd"));
        Assert.True(Correct(PronosticDe(pronostics, Spectateur2), "dofd"));
        Assert.Equal(JsonValueKind.Null, PronosticDe(pronostics, Spectateur).GetProperty("equipeGagnante").ValueKind);
    }

    [Fact]
    public async Task Un_spectateur_peut_changer_ou_retirer_son_pronostic_avant_la_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await ReadJsonAsync(await PronostiquerAsync(serie.SeriesId, Spectateur, 1, "A", null));
        await ReadJsonAsync(await PronostiquerAsync(serie.SeriesId, Spectateur, 1, "B", null));
        await ReadJsonAsync(await PronostiquerAsync(serie.SeriesId, Spectateur2, 1, "A", null));
        await ReadJsonAsync(await PronostiquerAsync(serie.SeriesId, Spectateur2, 1, null, null));
        var gameId = await PlayGameAsync(client, serie.SeriesId);

        var pronostics = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/pronostics", Ct));

        var seul = Assert.Single(pronostics.GetProperty("pronostics").EnumerateArray());
        Assert.Equal("B", seul.GetProperty("equipeGagnante").GetProperty("equipe").GetString());
    }

    [Fact]
    public async Task Les_pronostics_ne_donnent_ni_points_ni_classement()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await ReadJsonAsync(await PronostiquerAsync(serie.SeriesId, Spectateur, 1, "A", null, nom: "Zoé"));
        await ReadJsonAsync(await PronostiquerAsync(serie.SeriesId, Spectateur2, 1, "B", null, nom: "Alex"));
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");

        var pronostics = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/pronostics", Ct));

        Assert.Equal(["gameId", "gameNumber", "vainqueurConnu", "votesClos", "pronostics"],
            pronostics.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["pronostiqueurDiscordId", "pronostiqueurNom", "equipeGagnante", "dofd"],
            pronostics.GetProperty("pronostics")[0].EnumerateObject().Select(p => p.Name));
        // Listés par nom, pas par justesse.
        Assert.Equal(["Alex", "Zoé"], pronostics.GetProperty("pronostics").EnumerateArray()
            .Select(p => p.GetProperty("pronostiqueurNom").GetString()));
    }

    [Fact]
    public async Task Les_pronostics_d_une_serie_annulee_disparaissent_avec_elle()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await ReadJsonAsync(await PronostiquerAsync(serie.SeriesId, Spectateur, 1, "A", null));

        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));

        await AssertRefusAsync(await Factory.CreateBotClient().GetAsync($"/bot/series/{serie.SeriesId}/pronostics", Ct), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Une_serie_terminee_n_accepte_plus_de_pronostic()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, serie.SeriesId, "A");

        await AssertRefusAsync(await PronostiquerAsync(serie.SeriesId, Spectateur, 2, "A", null), HttpStatusCode.Conflict);
    }
}
