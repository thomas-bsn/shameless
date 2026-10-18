using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// Un Administrateur corrige les stats d'un joueur dans une game terminée : une correction par champ et
/// par joueur, stockée à part ; le JSON du Companion reste intact ; l'affichage, la note et le cost
/// prennent la valeur corrigée ; annuler la correction rétablit la valeur d'origine.
/// </summary>
public class CorrectionDeStatsTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static readonly DateTimeOffset Mars = new(2026, 3, 10, 20, 0, 0, TimeSpan.Zero);

    private int _rapports;

    private static Task<HttpResponseMessage> CorrigerAsync(HttpClient client, int gameId, int playerId, object corps) =>
        client.PutAsJsonAsync($"/games/{gameId}/players/{playerId}/corrections", corps, Ct);

    private static Task<HttpResponseMessage> AnnulerAsync(HttpClient client, int gameId, int playerId, string champ) =>
        client.DeleteAsync($"/games/{gameId}/players/{playerId}/corrections/{champ}", Ct);

    private async Task<JsonElement> JoueurAsync(HttpClient client, int gameId, int playerId)
    {
        var game = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}", Ct));
        return game.GetProperty("teamA").EnumerateArray().Concat(game.GetProperty("teamB").EnumerateArray())
            .Single(p => p.GetProperty("playerId").GetInt32() == playerId);
    }

    private async Task<int> JouerUneGameAsync(HttpClient client, int seriesId, string gagnante = "A")
    {
        var gameId = await PlayGameAsync(client, seriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, seriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport($"EUW1_8300{_rapports++:D5}", "blue", gagnante,
            donnees: new { gameDuration = 1800, gameMode = "CLASSIC" })));
        return gameId;
    }

    private async Task<decimal> CostAsync(HttpClient client, int playerId) =>
        (await ReadJsonAsync(await client.GetAsync($"/players/{playerId}/cost", Ct))).GetProperty("cost").GetDecimal();

    [Fact]
    public async Task Corriger_ou_annuler_une_stat_est_reserve_aux_administrateurs()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var gameId = await JouerUneGameAsync(client, serie.SeriesId);
        var a1 = serie.TeamA[0];

        Assert.Equal(HttpStatusCode.Forbidden, (await CorrigerAsync(client, gameId, a1, new { kills = 9 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await CorrigerAsync(Factory.CreateAnonymousClient(), gameId, a1, new { kills = 9 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AnnulerAsync(client, gameId, a1, "kills")).StatusCode);
        Assert.Equal(0, (await JoueurAsync(client, gameId, a1)).GetProperty("stats").GetProperty("kills").GetInt32());
    }

    [Fact]
    public async Task Une_correction_est_stockee_a_part_sans_toucher_au_json_du_companion_et_marque_la_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var gameId = await JouerUneGameAsync(client, serie.SeriesId);
        var a1 = serie.TeamA[0];
        var avant = await JsonBrutAsync(gameId, a1);

        var reponse = await CorrigerAsync(Factory.CreateAdminClient(), gameId, a1, new
        {
            champion = "Darius", position = "Top", kills = 12, deaths = 1, assists = 3, cs = 210,
            goldEarned = 15000, damageToChampions = 31000, visionScore = 40
        });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var joueur = await JoueurAsync(client, gameId, a1);
        Assert.Equal("Darius", joueur.GetProperty("championId").GetString());
        var stats = joueur.GetProperty("stats");
        Assert.Equal((12, 1, 3), (stats.GetProperty("kills").GetInt32(), stats.GetProperty("deaths").GetInt32(), stats.GetProperty("assists").GetInt32()));
        Assert.Equal(210, stats.GetProperty("minionsKilled").GetInt32() + stats.GetProperty("neutralMinionsKilled").GetInt32());
        Assert.Equal((15000, 31000, 40), (stats.GetProperty("goldEarned").GetInt32(), stats.GetProperty("damageToChampions").GetInt32(), stats.GetProperty("visionScore").GetInt32()));
        Assert.Equal("Top", stats.GetProperty("position").GetString());
        Assert.Equal("Top", joueur.GetProperty("performance").GetProperty("role").GetString());
        Assert.Equal(9, joueur.GetProperty("champsCorriges").GetArrayLength());
        // Un autre joueur n'est pas touché.
        Assert.Equal(1, (await JoueurAsync(client, gameId, serie.TeamA[1])).GetProperty("stats").GetProperty("kills").GetInt32());
        // La game porte la marque, et le JSON d'origine est intact.
        var game = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}", Ct));
        Assert.NotEqual(JsonValueKind.Null, game.GetProperty("modifieeParAdminLe").ValueKind);
        Assert.Equal(avant, await JsonBrutAsync(gameId, a1));
        Assert.Equal("Garen", await BrutChampionAsync(gameId, a1));
    }

    [Fact]
    public async Task Annuler_une_correction_retablit_la_valeur_d_origine()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient();
        var serie = await CreateSerieAsync(client);
        var gameId = await JouerUneGameAsync(client, serie.SeriesId);
        var a1 = serie.TeamA[0];
        await ReadJsonAsync(await CorrigerAsync(admin, gameId, a1, new { kills = 12, champion = "Darius", note = 91 }));

        await ReadJsonAsync(await AnnulerAsync(admin, gameId, a1, "kills"));
        var joueur = await JoueurAsync(client, gameId, a1);

        Assert.Equal(0, joueur.GetProperty("stats").GetProperty("kills").GetInt32());
        Assert.Equal("Darius", joueur.GetProperty("championId").GetString());
        Assert.Equal(91, joueur.GetProperty("performance").GetProperty("note").GetInt32());
        Assert.Equal(["champion", "note"], joueur.GetProperty("champsCorriges").EnumerateArray().Select(c => c.GetString()).Order());

        await ReadJsonAsync(await AnnulerAsync(admin, gameId, a1, "champion"));
        await ReadJsonAsync(await AnnulerAsync(admin, gameId, a1, "note"));
        joueur = await JoueurAsync(client, gameId, a1);
        Assert.Equal("Garen", joueur.GetProperty("championId").GetString());
        Assert.NotEqual(91, joueur.GetProperty("performance").GetProperty("note").GetInt32());
        Assert.Equal(0, joueur.GetProperty("champsCorriges").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await AnnulerAsync(admin, gameId, a1, "kills")).StatusCode);
    }

    [Fact]
    public async Task Corriger_une_stat_recalcule_la_note_la_fiche_et_le_cost_du_mois()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient();
        Factory.Clock.SetUtcNow(Mars);
        var serie = await CreateSerieAsync(client, format: 5);
        var games = new List<int>();
        foreach (var gagnante in new[] { "A", "B", "A", "B", "A" })
            games.Add(await JouerUneGameAsync(client, serie.SeriesId, gagnante));
        var a1 = serie.TeamA[0];
        var noteAvant = (await JoueurAsync(client, games[0], a1)).GetProperty("performance").GetProperty("note").GetInt32();
        var costAvant = await CostAsync(client, a1);

        await ReadJsonAsync(await CorrigerAsync(admin, games[0], a1, new { note = 100 }));
        await ReadJsonAsync(await CorrigerAsync(admin, games[1], a1, new { note = 100 }));
        await ReadJsonAsync(await CorrigerAsync(admin, games[2], a1, new { note = 100 }));

        Assert.Equal(100, (await JoueurAsync(client, games[0], a1)).GetProperty("performance").GetProperty("note").GetInt32());
        var fiche = await ReadJsonAsync(await client.GetAsync($"/players/{a1}/fiche", Ct));
        Assert.True(fiche.GetProperty("stats").GetProperty("noteMoyenne").GetDouble() > noteAvant);
        var costApres = await CostAsync(client, a1);
        Assert.True(costApres > costAvant, $"{costApres} doit dépasser {costAvant}");
        Assert.Equal(costApres, (await ReadJsonAsync(await client.GetAsync($"/players/{a1}", Ct))).GetProperty("currentCost").GetDecimal());

        foreach (var game in games[..3])
            await ReadJsonAsync(await AnnulerAsync(admin, game, a1, "note"));
        Assert.Equal(costAvant, await CostAsync(client, a1));
    }

    [Fact]
    public async Task Corriger_le_champion_et_la_voie_change_la_fiche_et_les_champions_joues()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, fearless: true);
        var gameId = await JouerUneGameAsync(client, serie.SeriesId);
        var a1 = serie.TeamA[0];

        await ReadJsonAsync(await CorrigerAsync(Factory.CreateAdminClient(), gameId, a1, new { champion = "Darius", position = "Support" }));

        var fiche = await ReadJsonAsync(await client.GetAsync($"/players/{a1}/fiche", Ct));
        Assert.Equal(["Darius"], fiche.GetProperty("champions").EnumerateArray().Select(c => c.GetProperty("championId").GetString()));
        Assert.Equal("Support", fiche.GetProperty("roles")[0].GetProperty("role").GetString());
        var lue = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        var joues = lue.GetProperty("games")[0].GetProperty("championsJoues").EnumerateArray()
            .Single(c => c.GetProperty("playerId").GetInt32() == a1);
        Assert.Equal("Darius", joues.GetProperty("championId").GetString());
    }

    [Theory]
    [InlineData("rien")]
    [InlineData("kills-negatif")]
    [InlineData("note-hors-plage")]
    [InlineData("voie-inconnue")]
    [InlineData("champion-vide")]
    public async Task Une_correction_invalide_repond_400_et_ne_change_rien(string cas)
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var gameId = await JouerUneGameAsync(client, serie.SeriesId);
        object corps = cas switch
        {
            "rien" => new { },
            "kills-negatif" => new { kills = -1 },
            "note-hors-plage" => new { note = 101 },
            "voie-inconnue" => new { position = "Banc" },
            _ => new { champion = " " }
        };

        await AssertRefusAsync(await CorrigerAsync(Factory.CreateAdminClient(), gameId, serie.TeamA[0], corps), HttpStatusCode.BadRequest);

        var game = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}", Ct));
        Assert.Equal(JsonValueKind.Null, game.GetProperty("modifieeParAdminLe").ValueKind);
    }

    [Fact]
    public async Task Corriger_une_game_en_attente_ou_un_joueur_absent_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient();
        var serie = await CreateSerieAsync(client);
        var terminee = await JouerUneGameAsync(client, serie.SeriesId);
        var enAttente = await PlayGameAsync(client, serie.SeriesId);

        await AssertRefusAsync(await CorrigerAsync(admin, enAttente, serie.TeamA[0], new { kills = 1 }), HttpStatusCode.Conflict);
        await AssertRefusAsync(await CorrigerAsync(admin, 99999, serie.TeamA[0], new { kills = 1 }), HttpStatusCode.NotFound);
        await AssertRefusAsync(await CorrigerAsync(admin, terminee, 99999, new { kills = 1 }), HttpStatusCode.NotFound);
        await AssertRefusAsync(await AnnulerAsync(admin, terminee, serie.TeamA[0], "inconnu"), HttpStatusCode.BadRequest);
    }

    private async Task<string?> JsonBrutAsync(int gameId, int playerId)
    {
        await using var db = Factory.CreateDbContext();
        return await db.CustomGameParticipants
            .Where(p => p.CustomGameId == gameId && p.PlayerId == playerId)
            .Select(p => p.RiotParticipantData).SingleAsync(Ct);
    }

    private async Task<string?> BrutChampionAsync(int gameId, int playerId)
    {
        await using var db = Factory.CreateDbContext();
        return await db.CustomGameParticipants
            .Where(p => p.CustomGameId == gameId && p.PlayerId == playerId)
            .Select(p => p.ChampionId).SingleAsync(Ct);
    }
}
