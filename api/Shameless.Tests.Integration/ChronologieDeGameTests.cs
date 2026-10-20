using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// La chronologie facultative du Rapport (ADR 0022) : points datés par joueur et événements d'objectifs,
/// validés, gardés et exposés dans le détail de la game avec la nature de chaque série ; l'or est
/// estimé côté API à partir de la valeur des objets et de l'or total final.
/// </summary>
public class ChronologieDeGameTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static readonly string[] Prenoms = ["A1", "A2", "A3", "A4", "A5", "B1", "B2", "B3", "B4", "B5"];

    private static object Joueur(string prenom, int kills, int cs, int valeurObjets, bool mort = false) => new
    {
        puuid = $"puuid-{prenom}#EUW", kills, deaths = 0, assists = 1, cs, level = 6, vision = 4, itemsValue = valeurObjets, dead = mort
    };

    /// <summary>Un point où chaque joueur a les mêmes valeurs, sauf A1 dont on choisit kills et valeur des objets.</summary>
    private static object Point(int t, int killsA1, int valeurA1) => new
    {
        t,
        joueurs = Prenoms.Select(p => p == "A1" ? Joueur(p, killsA1, 10 * t / 60, valeurA1) : Joueur(p, 0, 5, 1000)).ToArray()
    };

    private static object Chronologie(object[]? points = null, object[]? evenements = null) => new
    {
        points = points ?? [Point(0, 0, 0), Point(600, 2, 3000), Point(1200, 5, 6000)],
        evenements = evenements ?? [new { t = 300, type = "dragon", side = "blue" }, new { t = 900, type = "turret", side = "red" }]
    };

    /// <summary>Seul A1 a un or final dans ses stats.</summary>
    private static object Stats(int i) => i == 0
        ? new { kills = 5, deaths = 2, assists = 7, goldEarned = 12000 }
        : new { kills = 1, deaths = 2, assists = 7 };

    private static object RapportAvec(string id, object? chronologie, string[]? equipeA = null) => new
    {
        riotMatchId = id,
        donnees = new { gameDuration = 1200, gameMode = "CLASSIC", gameType = "CUSTOM_GAME" },
        joueurs = ((dynamic)Rapport(id, "blue", "A", equipeA: equipeA, statsDe: Stats)).joueurs,
        chronologie
    };

    private async Task<(HttpClient Client, CreatedSerie Serie, int GameId, string Code)> GameEnAttenteAsync()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        return (client, serie, gameId, await CodeCompanionAsync(client, serie.SeriesId));
    }

    private async Task<JsonElement> DetailAsync(HttpClient client, int gameId) =>
        await ReadJsonAsync(await client.GetAsync($"/games/{gameId}", Ct));

    [Fact]
    public async Task Un_Rapport_sans_chronologie_reste_valide_et_la_game_n_en_expose_pas()
    {
        var (client, _, gameId, code) = await GameEnAttenteAsync();

        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_7100000001", "blue", "A")));

        Assert.Equal(JsonValueKind.Null, (await DetailAsync(client, gameId)).GetProperty("chronologie").ValueKind);
    }

    [Fact]
    public async Task Un_Rapport_avec_chronologie_l_expose_avec_la_nature_de_chaque_serie()
    {
        var (client, _, gameId, code) = await GameEnAttenteAsync();

        await ReadJsonAsync(await EnvoyerRapportAsync(code, RapportAvec("EUW1_7100000002", Chronologie())));

        var chrono = (await DetailAsync(client, gameId)).GetProperty("chronologie");
        var natures = chrono.GetProperty("series").EnumerateArray()
            .ToDictionary(s => s.GetProperty("nom").GetString()!, s => s.GetProperty("nature").GetString());
        foreach (var reelle in new[] { "kills", "morts", "assists", "cs", "niveau", "vision", "objectifs" })
            Assert.Equal("reelle", natures[reelle]);
        Assert.Equal("estimee", natures["or"]);
        Assert.Equal("indisponible", natures["degats"]);
        Assert.Equal("indisponible", natures["xp"]);

        var points = chrono.GetProperty("points").EnumerateArray().ToList();
        Assert.Equal([0, 600, 1200], points.Select(p => p.GetProperty("t").GetInt32()));
        Assert.Equal(10, points[1].GetProperty("joueurs").GetArrayLength());
        var a1 = points[1].GetProperty("joueurs").EnumerateArray().Single(j => j.GetProperty("kills").GetInt32() == 2);
        Assert.Equal(6, a1.GetProperty("niveau").GetInt32());
        Assert.Equal(4, a1.GetProperty("vision").GetInt32());
        Assert.Equal(3000, a1.GetProperty("valeurObjets").GetInt32());
        Assert.False(a1.GetProperty("mort").GetBoolean());

        var evenements = chrono.GetProperty("evenements").EnumerateArray().ToList();
        Assert.Equal(("dragon", "blue", 300),
            (evenements[0].GetProperty("type").GetString(), evenements[0].GetProperty("side").GetString(), evenements[0].GetProperty("t").GetInt32()));
        Assert.Equal(("turret", "red"), (evenements[1].GetProperty("type").GetString(), evenements[1].GetProperty("side").GetString()));
    }

    [Fact]
    public async Task L_or_estime_remet_la_valeur_des_objets_a_l_echelle_de_l_or_final_et_reste_nul_sans_or_final()
    {
        var (client, serie, gameId, code) = await GameEnAttenteAsync();
        await ReadJsonAsync(await EnvoyerRapportAsync(code, RapportAvec("EUW1_7100000003", Chronologie())));

        var points = (await DetailAsync(client, gameId)).GetProperty("chronologie").GetProperty("points").EnumerateArray().ToList();

        // A1 : or final 12 000 pour 6 000 de valeur d'objets au dernier point, donc facteur 2.
        var a1 = points.Select(p => p.GetProperty("joueurs").EnumerateArray().Single(j => j.GetProperty("playerId").GetInt32() == serie.TeamA[0])).ToList();
        Assert.Equal([0, 6000, 12000], a1.Select(j => j.GetProperty("orEstime").GetInt32()));
        // Les autres n'ont pas d'or final dans leurs stats : rien n'est inventé.
        var autre = points[1].GetProperty("joueurs").EnumerateArray().Single(j => j.GetProperty("playerId").GetInt32() == serie.TeamA[1]);
        Assert.Equal(JsonValueKind.Null, autre.GetProperty("orEstime").ValueKind);
    }

    [Fact]
    public async Task Sans_aucun_or_estimable_la_serie_or_est_indisponible()
    {
        var (client, _, gameId, code) = await GameEnAttenteAsync();
        var rapport = new
        {
            riotMatchId = "EUW1_7100000004",
            donnees = new { gameDuration = 1200 },
            joueurs = ((dynamic)Rapport("EUW1_7100000004", "blue", "A")).joueurs,
            chronologie = Chronologie()
        };
        await ReadJsonAsync(await EnvoyerRapportAsync(code, rapport));

        var series = (await DetailAsync(client, gameId)).GetProperty("chronologie").GetProperty("series").EnumerateArray();

        Assert.Equal("indisponible", series.Single(s => s.GetProperty("nom").GetString() == "or").GetProperty("nature").GetString());
    }

    public static IEnumerable<object[]> ChronologiesInvalides()
    {
        yield return ["sans points", Chronologie(points: [])];
        yield return ["temps qui recule", Chronologie(points: [Point(600, 0, 0), Point(300, 1, 0)])];
        yield return ["même instant deux fois", Chronologie(points: [Point(600, 0, 0), Point(600, 1, 0)])];
        yield return ["temps négatif", Chronologie(points: [Point(-1, 0, 0)])];
        yield return ["joueur inconnu du rapport", Chronologie(points: [new { t = 0, joueurs = new[] { Joueur("Z9", 0, 0, 0) } }])];
        yield return ["joueur en double dans un point", Chronologie(points: [new { t = 0, joueurs = new[] { Joueur("A1", 0, 0, 0), Joueur("A1", 0, 0, 0) } }])];
        yield return ["valeur négative", Chronologie(points: [new { t = 0, joueurs = new[] { Joueur("A1", -1, 0, 0) } }])];
        yield return ["niveau hors limites", Chronologie(points: [new { t = 0, joueurs = new object[] { new { puuid = "puuid-A1#EUW", kills = 0, deaths = 0, assists = 0, cs = 0, level = 19, vision = 0, itemsValue = 0, dead = false } } }])];
        yield return ["champ manquant", Chronologie(points: [new { t = 0, joueurs = new object[] { new { puuid = "puuid-A1#EUW", kills = 0, deaths = 0, assists = 0, cs = 0, level = 1, itemsValue = 0, dead = false } } }])];
        yield return ["type d'événement inconnu", Chronologie(evenements: [new { t = 10, type = "kraken", side = "blue" }])];
        yield return ["side d'événement inconnu", Chronologie(evenements: [new { t = 10, type = "dragon", side = "green" }])];
        yield return ["événement daté avant zéro", Chronologie(evenements: [new { t = -5, type = "dragon", side = "blue" }])];
    }

    [Theory]
    [MemberData(nameof(ChronologiesInvalides))]
    public async Task Une_chronologie_invalide_refuse_tout_le_Rapport(string cas, object chronologie)
    {
        var (client, _, gameId, code) = await GameEnAttenteAsync();

        await AssertRefusAsync(await EnvoyerRapportAsync(code, RapportAvec("EUW1_7100000005", chronologie)), HttpStatusCode.BadRequest);

        Assert.True((await DetailAsync(client, gameId)).GetProperty("status").GetString() == "pending", cas);
    }

    [Fact]
    public async Task Une_chronologie_sans_evenement_est_acceptee()
    {
        var (client, _, gameId, code) = await GameEnAttenteAsync();

        await ReadJsonAsync(await EnvoyerRapportAsync(code, RapportAvec("EUW1_7100000006", Chronologie(evenements: []))));

        Assert.Equal(0, (await DetailAsync(client, gameId)).GetProperty("chronologie").GetProperty("evenements").GetArrayLength());
    }

    [Fact]
    public async Task La_chronologie_d_un_Rapport_mis_en_revue_est_gardee_quand_l_organisateur_l_accepte()
    {
        var (client, serie, gameId, code) = await GameEnAttenteAsync();
        await CreatePlayerAsync(client, "X1");

        var recu = await ReadJsonAsync(await EnvoyerRapportAsync(code,
            RapportAvec("EUW1_7100000007", Chronologie(points: [new { t = 0, joueurs = new[] { Joueur("A1", 0, 0, 0) } }]),
                equipeA: ["A1", "A2", "A3", "A4", "X1"])));
        Assert.Equal("EnRevue", recu.GetProperty("statut").GetString());
        var revue = (await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct))).GetProperty("games")[0].GetProperty("rapportEnRevue");

        await ReadJsonAsync(await client.PostAsync($"/rapports/{revue.GetProperty("id").GetInt32()}/accepter", null, Ct));

        Assert.Equal(1, (await DetailAsync(client, gameId)).GetProperty("chronologie").GetProperty("points").GetArrayLength());
    }

    [Fact]
    public async Task L_import_d_un_Rapport_donne_sa_chronologie_a_une_game_terminee()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient();
        var serie = await CreateSerieAsync(client);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A", teamASide: "blue");
        var rapport = new
        {
            riotMatchId = "EUW1_7100000008",
            donnees = new { gameDuration = 1200 },
            joueurs = ((dynamic)Rapport("EUW1_7100000008", "blue", "A")).joueurs,
            chronologie = Chronologie()
        };

        var reponse = await admin.PostAsJsonAsync($"/games/{gameId}/import-rapport", rapport, Ct);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(3, (await DetailAsync(client, gameId)).GetProperty("chronologie").GetProperty("points").GetArrayLength());
    }
}
