using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// Ce que la page d'une game (<c>GET /games/{id}</c>) dit de chaque joueur : rôle joué et sa source,
/// objets, objet de rôle, sorts, runes, valeurs par minute et parts d'équipe, place et badges (#170).
/// </summary>
public class DetailsDuJoueurTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const int Duree = 1500; // 25 minutes

    // Équipe A (gagnante) : dégâts 22000+10000+8000+6000+4000 = 50000, or 12500+11000+10000+9500+7000 = 50000, kills 9+3+2+1+0 = 15.
    private static readonly int[] DegatsA = [22000, 10000, 8000, 6000, 4000];
    private static readonly int[] OrA = [12500, 11000, 10000, 9500, 7000];
    private static readonly int[] KillsA = [9, 3, 2, 1, 0];

    private async Task<JsonElement> GameAsync(Func<int, object>? statsDe = null, string gagnante = "A", object? donnees = null)
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_7200000001", "blue", gagnante,
            statsDe: statsDe ?? StatsParDefaut,
            donnees: donnees ?? new { gameDuration = Duree, gameMode = "CLASSIC", gameType = "CUSTOM_GAME" })));
        return await ReadJsonAsync(await client.GetAsync($"/games/{gameId}", Ct));
    }

    private static object StatsParDefaut(int i) => i < 5
        ? new
        {
            kills = KillsA[i], deaths = 2, assists = i == 0 ? 5 : 1, totalMinionsKilled = i == 0 ? 200 : 100,
            neutralMinionsKilled = i == 0 ? 25 : 0, totalDamageDealtToChampions = DegatsA[i], goldEarned = OrA[i],
            visionScore = i == 0 ? 25 : 10, teamPosition = i == 0 ? "MIDDLE" : "TOP",
            items = i == 0 ? new[] { 6655, 3020, 3157, 0, 0, 0, 3340 } : new[] { 0, 0, 0, 0, 0, 0, 0 },
            spell1Id = 4, spell2Id = 14, roleBoundItem = i == 0 ? 1203 : 1206,
            perk0 = 8112, perk1 = 8126, perk2 = 8138, perk3 = 8135, perk4 = 8226, perk5 = 8210,
            perkPrimaryStyle = 8100, perkSubStyle = 8200
        }
        : new
        {
            kills = 1, deaths = 4, assists = 2, totalMinionsKilled = 120, neutralMinionsKilled = 0,
            totalDamageDealtToChampions = 9000, goldEarned = 8000, visionScore = 12, teamPosition = "UTILITY",
            items = new[] { 0, 0, 0, 0, 0, 0, 0 }, spell1Id = 4, spell2Id = 3, roleBoundItem = 0,
            perk0 = 0, perk1 = 0, perk2 = 0, perk3 = 0, perk4 = 0, perk5 = 0, perkPrimaryStyle = 0, perkSubStyle = 0
        };

    private static JsonElement Joueur(JsonElement game, string equipe, int rang) =>
        game.GetProperty(equipe).EnumerateArray().ElementAt(rang);

    [Fact]
    public async Task Une_game_lue_donne_role_objets_objet_de_role_sorts_et_runes_de_chaque_joueur()
    {
        var game = await GameAsync();

        var d = Joueur(game, "teamA", 0).GetProperty("details");
        Assert.Equal("Mid", d.GetProperty("role").GetString());
        Assert.Equal("Partie", d.GetProperty("sourceDuRole").GetString());
        Assert.Equal([6655, 3020, 3157, 0, 0, 0, 3340], d.GetProperty("items").EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal(1203, d.GetProperty("objetDeRole").GetProperty("id").GetInt32());
        Assert.Equal("EnCours", d.GetProperty("objetDeRole").GetProperty("etat").GetString());
        Assert.Equal([4, 14], [d.GetProperty("spell1Id").GetInt32(), d.GetProperty("spell2Id").GetInt32()]);
        Assert.Equal([8112, 8126, 8138, 8135, 8226, 8210], d.GetProperty("perks").EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal(8100, d.GetProperty("perkPrimaryStyle").GetInt32());
        Assert.Equal(8200, d.GetProperty("perkSubStyle").GetInt32());
    }

    [Fact]
    public async Task Une_quete_de_role_terminee_est_signalee_et_un_joueur_sans_objet_de_role_n_en_a_pas()
    {
        var game = await GameAsync();

        Assert.Equal("Terminee", Joueur(game, "teamA", 1).GetProperty("details").GetProperty("objetDeRole").GetProperty("etat").GetString());
        Assert.Equal(JsonValueKind.Null, Joueur(game, "teamB", 0).GetProperty("details").GetProperty("objetDeRole").ValueKind);
    }

    private static string? Badge(JsonElement joueur) => joueur.GetProperty("badge").GetString();

    private static IEnumerable<JsonElement> Tous(JsonElement game) =>
        game.GetProperty("teamA").EnumerateArray().Concat(game.GetProperty("teamB").EnumerateArray());

    [Fact]
    public async Task Chaque_joueur_a_sa_place_de_1_a_10_d_apres_sa_note()
    {
        var game = await GameAsync();

        var places = Tous(game).Select(j => (Place: j.GetProperty("place").GetInt32(), Note: j.GetProperty("performance").GetProperty("note").GetInt32())).ToList();
        Assert.Equal(Enumerable.Range(1, 10), places.Select(p => p.Place).Order());
        Assert.Equal(places.OrderBy(p => p.Place).Select(p => p.Note), places.Select(p => p.Note).OrderDescending());
        Assert.Equal(1, Joueur(game, "teamA", 0).GetProperty("place").GetInt32());
    }

    [Fact]
    public async Task La_meilleure_perf_va_a_la_meilleure_note_de_l_equipe_gagnante_et_l_ace_a_celle_de_l_equipe_perdante()
    {
        var game = await GameAsync(gagnante: "A");

        Assert.Equal("MeilleurePerf", Badge(Joueur(game, "teamA", 0)));
        Assert.Equal(["A1"], Tous(game).Where(j => Badge(j) == "MeilleurePerf").Select(j => j.GetProperty("prenom").GetString()));
        var ace = Tous(game).Where(j => Badge(j) == "Ace").ToList();
        var meilleurDeB = game.GetProperty("teamB").EnumerateArray().MinBy(j => j.GetProperty("place").GetInt32());
        Assert.Equal([meilleurDeB.GetProperty("prenom").GetString()], ace.Select(j => j.GetProperty("prenom").GetString()));
    }

    [Fact]
    public async Task Quand_l_equipe_B_gagne_le_meilleur_de_A_porte_l_ace()
    {
        var game = await GameAsync(gagnante: "B");

        Assert.Equal("Ace", Badge(Joueur(game, "teamA", 0)));
        Assert.Single(Tous(game), j => Badge(j) == "MeilleurePerf");
        Assert.Equal("MeilleurePerf", Badge(game.GetProperty("teamB").EnumerateArray().MinBy(j => j.GetProperty("place").GetInt32())));
    }

    [Fact]
    public async Task Un_joueur_sans_note_n_a_ni_place_ni_badge()
    {
        var game = await GameAsync(i => i == 9 ? new Dictionary<string, object>() : StatsParDefaut(i));

        var sansNote = Joueur(game, "teamB", 4);
        Assert.Equal(JsonValueKind.Null, sansNote.GetProperty("performance").ValueKind);
        Assert.Equal(JsonValueKind.Null, sansNote.GetProperty("place").ValueKind);
        Assert.Equal(JsonValueKind.Null, sansNote.GetProperty("badge").ValueKind);
        Assert.Equal(9, Tous(game).Count(j => j.GetProperty("place").ValueKind == JsonValueKind.Number));
    }

    [Fact]
    public async Task Les_objectifs_des_deux_equipes_sont_lus_dans_les_donnees_du_Rapport()
    {
        var game = await GameAsync(donnees: new
        {
            gameDuration = Duree,
            teams = new object[]
            {
                new { teamId = 100, dragonKills = 3, baronKills = 1, riftHeraldKills = 1, hordeKills = 3, towerKills = 11, inhibitorKills = 2, firstBlood = false, firstTower = true, firstInhibitor = true },
                new { teamId = 200, dragonKills = 0, baronKills = 0, towerKills = 2, firstBlood = true }
            }
        });

        var a = game.GetProperty("teamAObjectifs");
        Assert.Equal([3, 1, 1, 3, 11, 2], new[] { "dragonKills", "baronKills", "riftHeraldKills", "hordeKills", "towerKills", "inhibitorKills" }.Select(n => a.GetProperty(n).GetInt32()));
        Assert.True(a.GetProperty("firstTower").GetBoolean());
        Assert.False(a.GetProperty("firstBlood").GetBoolean());
        var b = game.GetProperty("teamBObjectifs");
        Assert.Equal(2, b.GetProperty("towerKills").GetInt32());
        Assert.True(b.GetProperty("firstBlood").GetBoolean());
        Assert.Equal(JsonValueKind.Null, b.GetProperty("hordeKills").ValueKind);
    }

    [Fact]
    public async Task Une_game_saisie_a_la_main_reste_lisible_sans_details_ni_place()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");

        var game = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}", Ct));

        Assert.Equal(JsonValueKind.Null, game.GetProperty("teamAObjectifs").ValueKind);
        var d = Joueur(game, "teamA", 0).GetProperty("details");
        Assert.Equal(JsonValueKind.Null, d.GetProperty("items").ValueKind);
        Assert.Equal(JsonValueKind.Null, d.GetProperty("csParMinute").ValueKind);
        Assert.Equal(JsonValueKind.Null, Joueur(game, "teamA", 0).GetProperty("place").ValueKind);
    }

    [Fact]
    public async Task Un_id_inconnu_du_catalogue_ne_fait_pas_echouer_la_lecture()
    {
        var game = await GameAsync(i => i == 0
            ? new Dictionary<string, object> { ["kills"] = 3, ["items"] = new[] { 999999, 0, 0, 0, 0, 0, 0 }, ["spell1Id"] = 99999, ["roleBoundItem"] = 777777, ["perk0"] = 1 }
            : StatsParDefaut(i));

        var d = Joueur(game, "teamA", 0).GetProperty("details");
        Assert.Equal(999999, d.GetProperty("items")[0].GetInt32());
        Assert.Equal(777777, d.GetProperty("objetDeRole").GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Null, d.GetProperty("objetDeRole").GetProperty("etat").ValueKind);
    }

    [Fact]
    public async Task Les_valeurs_derivees_suivent_les_corrections_d_un_administrateur()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_7200000002", "blue", "A",
            statsDe: StatsParDefaut, donnees: new { gameDuration = Duree })));
        var admin = Factory.CreateAdminClient();
        await ReadJsonAsync(await admin.PutAsJsonAsync($"/games/{gameId}/players/{serie.TeamA[0]}/corrections", new { goldEarned = 25000 }, Ct));

        var game = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}", Ct));

        var d = Joueur(game, "teamA", 0).GetProperty("details");
        Assert.Equal(1000.0, d.GetProperty("orParMinute").GetDouble());  // 25000 / 25
        Assert.Equal(0.4, d.GetProperty("partDeLOr").GetDouble());       // 25000 / (50000 - 12500 + 25000)
    }

    [Fact]
    public async Task Les_valeurs_par_minute_et_les_parts_d_equipe_sont_calculees_sur_un_cas_chiffre()
    {
        var game = await GameAsync();

        var d = Joueur(game, "teamA", 0).GetProperty("details");
        Assert.Equal(9.0, d.GetProperty("csParMinute").GetDouble());       // 225 CS / 25 min
        Assert.Equal(500.0, d.GetProperty("orParMinute").GetDouble());     // 12500 / 25
        Assert.Equal(880.0, d.GetProperty("degatsParMinute").GetDouble()); // 22000 / 25
        Assert.Equal(1.0, d.GetProperty("visionParMinute").GetDouble());   // 25 / 25
        Assert.Equal(0.44, d.GetProperty("partDesDegats").GetDouble());    // 22000 / 50000
        Assert.Equal(0.25, d.GetProperty("partDeLOr").GetDouble());        // 12500 / 50000
        Assert.Equal(0.6, d.GetProperty("partDesKills").GetDouble());      // 9 / 15
    }
}
