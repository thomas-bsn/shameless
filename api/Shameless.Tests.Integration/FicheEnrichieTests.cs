using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// La fiche enrichie d'un Joueur (#175) : « Joué avec », résumé des 30 dernières games, liste des games
/// groupée par soirée ou par jour et filtrable. Jeu de données chiffré, vu par A1 :
/// soirée 1 (jour 1), Série S1 : G1 victoire Garen Top 4/2/6, G2 victoire Teemo Jungle 1/5/2 ;
/// soirée 2 (jour 2), Série S2 où A1 joue avec A2, B1, B2, B3 : G3 victoire Garen Top 10/0/5,
/// G4 défaite Garen Adc 7/3/4. A1 est le meilleur de son équipe en G1 et G3 (Meilleure perf) et en G4 (ACE).
/// B1 vote A1 MVP en G1 et G3, et A1 DOFD en G1.
/// </summary>
public class FicheEnrichieTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const string DiscordB1 = "100000000000000006";
    private DateTimeOffset _jour1;

    private sealed record Jeu(int A1, int[] Ids, int S1, int S2, int G1, int G2, int G3, int G4, HttpClient Client);

    private static object Faible() => new { kills = 0, deaths = 6, assists = 0 };

    private async Task<Jeu> JeuAsync()
    {
        var client = Factory.CreateSignedInClient();
        _jour1 = new DateTimeOffset(Factory.Clock.GetUtcNow().UtcDateTime.Date.AddDays(1).AddHours(20), TimeSpan.Zero);
        Factory.Clock.SetUtcNow(_jour1);
        var s1 = await CreateSerieAsync(client, format: 5);
        var ids = s1.TeamA.Concat(s1.TeamB).ToArray(); // A1..A5, B1..B5
        await LierADiscordAsync(ids[5], DiscordB1);
        var code1 = await CodeCompanionAsync(client, s1.SeriesId);

        var g1 = await PlayGameAsync(client, s1.SeriesId, teamASide: "blue");
        await ReadJsonAsync(await EnvoyerRapportAsync(code1, Rapport("EUW1_8000000001", "blue", "A",
            statsDe: i => i == 0
                ? new { kills = 4, deaths = 2, assists = 6, totalMinionsKilled = 150, neutralMinionsKilled = 30, teamPosition = "TOP" }
                : i < 5 ? Faible() : new { kills = 1, deaths = 1, assists = 2 },
            donnees: new { gameDuration = 1800, gameMode = "CLASSIC" })));
        await VoterAsync(g1, mvp: ids[0], dofd: ids[0]);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        var g2 = await PlayGameAsync(client, s1.SeriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code1, Rapport("EUW1_8000000002", "red", "A",
            champions: ["Teemo", .. ChampionsA[1..], .. ChampionsB],
            statsDe: i => i == 0
                ? new { kills = 1, deaths = 5, assists = 2, totalMinionsKilled = 100, neutralMinionsKilled = 20, teamPosition = "JUNGLE" }
                : new { kills = 1, deaths = 1, assists = 2 },
            donnees: new { gameDuration = 1200, gameMode = "CLASSIC" })));
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        await PlayGameAsync(client, s1.SeriesId); // lancée, jamais terminée
        await ReadJsonAsync(await client.PostAsync($"/series/{s1.SeriesId}/stop", null, Ct));
        var soiree1 = (await ReadJsonAsync(await client.GetAsync("/soirees/open", Ct))).GetProperty("soiree").GetProperty("id").GetInt32();
        await ReadJsonAsync(await client.PostAsync($"/soirees/{soiree1}/close", null, Ct));

        Factory.Clock.SetUtcNow(_jour1.AddDays(1));
        await OpenSoireeAsync(client);
        int[] teamA = [ids[0], ids[1], ids[5], ids[6], ids[7]], teamB = [ids[2], ids[3], ids[4], ids[8], ids[9]];
        var s2 = await ReadJsonAsync(await client.PostAsJsonAsync("/series", new { teamA, teamB, format = 5, fearless = false }, Ct));
        var s2Id = s2.GetProperty("seriesId").GetInt32();
        var code2 = await CodeCompanionAsync(client, s2Id);
        string[] equipeA = ["A1", "A2", "B1", "B2", "B3"], equipeB = ["A3", "A4", "A5", "B4", "B5"];

        var g3 = await PlayGameAsync(client, s2Id, teamASide: "blue");
        await ReadJsonAsync(await EnvoyerRapportAsync(code2, Rapport("EUW1_8000000003", "blue", "A", equipeA, equipeB,
            statsDe: i => i == 0
                ? new { kills = 10, deaths = 0, assists = 5, totalMinionsKilled = 200, neutralMinionsKilled = 0, teamPosition = "TOP" }
                : i < 5 ? Faible() : new { kills = 1, deaths = 1, assists = 2 },
            donnees: new { gameDuration = 1500, gameMode = "CLASSIC" })));
        await VoterAsync(g3, mvp: ids[0], dofd: null);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        var g4 = await PlayGameAsync(client, s2Id);
        await ReadJsonAsync(await EnvoyerRapportAsync(code2, Rapport("EUW1_8000000004", "red", "B", equipeA, equipeB,
            statsDe: i => i == 0
                ? new { kills = 7, deaths = 3, assists = 4, totalMinionsKilled = 120, neutralMinionsKilled = 0, teamPosition = "BOTTOM" }
                : i < 5 ? Faible() : new { kills = 3, deaths = 1, assists = 2 },
            donnees: new { gameDuration = 1200, gameMode = "CLASSIC" })));
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        return new Jeu(ids[0], ids, s1.SeriesId, s2Id, g1, g2, g3, g4, client);
    }

    private async Task VoterAsync(int gameId, int mvp, int? dofd)
    {
        await ReadJsonAsync(await Factory.CreateBotClient().PutAsJsonAsync($"/bot/games/{gameId}/votes",
            new { votantDiscordId = DiscordB1, mvpPlayerId = mvp, dofdPlayerId = dofd }, Ct));
    }

    private async Task<JsonElement> FicheAsync(Jeu jeu, string requete = "") =>
        await ReadJsonAsync(await jeu.Client.GetAsync($"/players/{jeu.A1}/fiche{requete}", Ct));

    private static List<int> Ids(JsonElement groupe) =>
        groupe.GetProperty("games").EnumerateArray().Select(g => g.GetProperty("gameId").GetInt32()).ToList();

    private static (int, int, int, double) Bilan(JsonElement c) => (c.GetProperty("parties").GetInt32(),
        c.GetProperty("victoires").GetInt32(), c.GetProperty("defaites").GetInt32(), c.GetProperty("winrate").GetDouble());

    [Fact]
    public async Task La_fiche_donne_avec_qui_le_joueur_a_joue_parties_victoires_defaites_et_winrate()
    {
        var jeu = await JeuAsync();

        var joueAvec = (await FicheAsync(jeu)).GetProperty("joueAvec").EnumerateArray().ToList();

        // A2 : les 4 games (3 victoires) ; A3 à A5 : S1 seulement (2 victoires) ; B1 à B3 : S2 (1 victoire, 1 défaite).
        Assert.Equal(["A2", "A3", "A4", "A5", "B1", "B2", "B3"], joueAvec.Select(c => c.GetProperty("prenom").GetString()));
        Assert.Equal(jeu.Ids[1], joueAvec[0].GetProperty("playerId").GetInt32());
        Assert.Equal((4, 3, 1, 75.0), Bilan(joueAvec[0]));
        Assert.Equal((2, 2, 0, 100.0), Bilan(joueAvec[1]));
        Assert.Equal((2, 1, 1, 50.0), Bilan(joueAvec[4]));
    }

    [Fact]
    public async Task Le_resume_des_dernieres_games_donne_winrate_top_champions_kda_note_badges_et_titres()
    {
        var jeu = await JeuAsync();

        var fiche = await FicheAsync(jeu);
        var resume = fiche.GetProperty("resume");

        Assert.Equal(4, resume.GetProperty("games").GetInt32());
        Assert.Equal(3, resume.GetProperty("victoires").GetInt32());
        Assert.Equal(1, resume.GetProperty("defaites").GetInt32());
        Assert.Equal(75.0, resume.GetProperty("winrate").GetDouble());
        Assert.Equal(3.9, resume.GetProperty("kda").GetDouble()); // (22 + 17) / 10
        var top = resume.GetProperty("topChampions").EnumerateArray().ToList();
        Assert.Equal(["Garen", "Teemo"], top.Select(c => c.GetProperty("championId").GetString()));
        Assert.Equal((3, 66.7, 7.2), (top[0].GetProperty("games").GetInt32(), top[0].GetProperty("winrate").GetDouble(), top[0].GetProperty("kda").GetDouble()));
        Assert.Equal((1, 100.0, 0.6), (top[1].GetProperty("games").GetInt32(), top[1].GetProperty("winrate").GetDouble(), top[1].GetProperty("kda").GetDouble()));
        Assert.Equal(fiche.GetProperty("stats").GetProperty("noteMoyenne").GetDouble(), resume.GetProperty("noteMoyenne").GetDouble());
        Assert.Equal(2, resume.GetProperty("meilleuresPerfs").GetInt32()); // G1 et G3
        Assert.Equal(1, resume.GetProperty("aces").GetInt32()); // G4
        Assert.Equal(2, resume.GetProperty("mvp").GetInt32());
        Assert.Equal(1, resume.GetProperty("dofd").GetInt32());
    }

    [Fact]
    public async Task La_liste_a_plat_donne_pour_chaque_game_la_note_la_place_le_badge_les_kda_le_role_l_equipe_et_le_resultat()
    {
        var jeu = await JeuAsync();

        var fiche = await FicheAsync(jeu);

        var groupes = fiche.GetProperty("liste").EnumerateArray().ToList();
        var groupe = Assert.Single(groupes);
        Assert.Equal([jeu.G4, jeu.G3, jeu.G2, jeu.G1], Ids(groupe));
        var lignes = groupe.GetProperty("games").EnumerateArray().ToList();
        var (g4, g3, g2, g1) = (lignes[0], lignes[1], lignes[2], lignes[3]);
        Assert.Equal("MeilleurePerf", g3.GetProperty("badge").GetString());
        Assert.Equal(1, g3.GetProperty("place").GetInt32());
        Assert.Equal("MeilleurePerf", g1.GetProperty("badge").GetString());
        Assert.Equal("Ace", g4.GetProperty("badge").GetString());
        Assert.Equal(JsonValueKind.Null, g2.GetProperty("badge").ValueKind);
        Assert.Equal(("Adc", "A", false, 7, 3, 4), (g4.GetProperty("role").GetString(), g4.GetProperty("equipe").GetString(),
            g4.GetProperty("victoire").GetBoolean(), g4.GetProperty("stats").GetProperty("kills").GetInt32(),
            g4.GetProperty("stats").GetProperty("deaths").GetInt32(), g4.GetProperty("stats").GetProperty("assists").GetInt32()));
        Assert.InRange(g3.GetProperty("performance").GetProperty("note").GetInt32(), 0, 100);
    }

    [Fact]
    public async Task La_liste_se_groupe_par_soiree_ou_par_jour_de_la_plus_recente_a_la_plus_ancienne()
    {
        var jeu = await JeuAsync();

        foreach (var regroupement in new[] { "soiree", "jour" })
        {
            var groupes = (await FicheAsync(jeu, $"?regroupement={regroupement}")).GetProperty("liste").EnumerateArray().ToList();
            Assert.Equal(2, groupes.Count);
            Assert.Equal([jeu.G4, jeu.G3], Ids(groupes[0]));
            Assert.Equal([jeu.G2, jeu.G1], Ids(groupes[1]));
        }
        var jours = (await FicheAsync(jeu, "?regroupement=jour")).GetProperty("liste").EnumerateArray()
            .Select(g => g.GetProperty("jour").GetString()).ToList();
        Assert.Equal([_jour1.AddDays(1).ToString("yyyy-MM-dd"), _jour1.ToString("yyyy-MM-dd")], jours);
        var soirees = (await FicheAsync(jeu, "?regroupement=soiree")).GetProperty("liste").EnumerateArray()
            .Select(g => g.GetProperty("soireeId").GetInt32()).ToList();
        Assert.Equal(2, soirees.Distinct().Count());
    }

    [Theory]
    [InlineData("role=Top", new[] { 3, 1 })]
    [InlineData("role=Jungle", new[] { 2 })]
    [InlineData("champion=Teemo", new[] { 2 })]
    [InlineData("resultat=Defaite", new[] { 4 })]
    [InlineData("resultat=Victoire&champion=Garen", new[] { 3, 1 })]
    [InlineData("serieId=S1", new[] { 2, 1 })]
    [InlineData("serieId=S2&role=Adc", new[] { 4 })]
    public async Task La_liste_se_filtre_par_role_serie_champion_et_resultat(string requete, int[] numeros)
    {
        var jeu = await JeuAsync();
        var games = new[] { 0, jeu.G1, jeu.G2, jeu.G3, jeu.G4 };
        requete = requete.Replace("serieId=S1", $"serieId={jeu.S1}").Replace("serieId=S2", $"serieId={jeu.S2}");

        var fiche = await FicheAsync(jeu, $"?{requete}");

        Assert.Equal(numeros.Select(n => games[n]), fiche.GetProperty("liste").EnumerateArray().SelectMany(Ids));
        // Le filtre ne touche que la liste : le reste de la fiche est inchangé.
        Assert.Equal(4, fiche.GetProperty("games").GetArrayLength());
        Assert.Equal(4, fiche.GetProperty("resume").GetProperty("games").GetInt32());
    }

    [Fact]
    public async Task Un_joueur_sans_game_a_des_blocs_vides()
    {
        var client = Factory.CreateSignedInClient();
        var playerId = await CreatePlayerAsync(client, "Sam");

        var fiche = await ReadJsonAsync(await client.GetAsync($"/players/{playerId}/fiche?regroupement=soiree&role=Top", Ct));

        Assert.Empty(fiche.GetProperty("joueAvec").EnumerateArray());
        Assert.Empty(fiche.GetProperty("liste").EnumerateArray());
        var resume = fiche.GetProperty("resume");
        Assert.Equal(0, resume.GetProperty("games").GetInt32());
        Assert.Equal(JsonValueKind.Null, resume.GetProperty("winrate").ValueKind);
        Assert.Equal(JsonValueKind.Null, resume.GetProperty("kda").ValueKind);
        Assert.Equal(JsonValueKind.Null, resume.GetProperty("noteMoyenne").ValueKind);
        Assert.Empty(resume.GetProperty("topChampions").EnumerateArray());
        Assert.Equal(0, resume.GetProperty("meilleuresPerfs").GetInt32());
        Assert.Equal(0, resume.GetProperty("mvp").GetInt32());
    }
}
