using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// L'historique des games donne, pour chaque game terminée, les données du match et de ses dix
/// participants envoyées par le Companion (League Client API locale) : c'est la matière des stats,
/// des notes et du cost de la Phase 1c.
/// </summary>
public class HistoriqueDesGamesTests(ApiFactory factory) : IntegrationTest(factory)
{
    private async Task<List<JsonElement>> GamesDuMoisAsync(HttpClient client)
    {
        var now = Factory.Clock.GetUtcNow();
        var history = await ReadJsonAsync(await client.GetAsync($"/games/history/{now.Year}/{now.Month}", Ct));
        return history.GetProperty("games").EnumerateArray()
            .SelectMany(s => s.GetProperty("games").EnumerateArray())
            .ToList();
    }

    private static Dictionary<int, JsonElement> ParticipantsParJoueur(JsonElement game) =>
        game.GetProperty("teamA").EnumerateArray()
            .Concat(game.GetProperty("teamB").EnumerateArray())
            .ToDictionary(p => p.GetProperty("playerId").GetInt32());

    [Fact]
    public async Task L_historique_donne_les_donnees_du_match_et_de_chaque_participant_envoyees_par_le_companion()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "red");
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_9000000001", teamASide: "red", gagnante: "B")));

        var game = Assert.Single(await GamesDuMoisAsync(client));
        Assert.Equal(gameId, game.GetProperty("id").GetInt32());
        Assert.Equal("EUW1_9000000001", game.GetProperty("riotMatchId").GetString());
        var match = game.GetProperty("match");
        Assert.Equal(1834, match.GetProperty("durationSeconds").GetInt32());
        Assert.Equal("CLASSIC", match.GetProperty("gameMode").GetString());
        Assert.Equal("CUSTOM_GAME", match.GetProperty("gameType").GetString());

        var participants = ParticipantsParJoueur(game);
        Assert.Equal(10, participants.Count);
        var a1 = participants[serie.TeamA[0]];
        Assert.Equal("A1", a1.GetProperty("prenom").GetString());
        Assert.Equal("Garen", a1.GetProperty("championId").GetString());
        Assert.Equal(0, a1.GetProperty("stats").GetProperty("kills").GetInt32());
        Assert.Equal(2, a1.GetProperty("stats").GetProperty("deaths").GetInt32());
        Assert.Equal(7, a1.GetProperty("stats").GetProperty("assists").GetInt32());
        Assert.Equal(12000, a1.GetProperty("stats").GetProperty("damageToChampions").GetInt32());
        var b5 = participants[serie.TeamB[4]];
        Assert.Equal("Nami", b5.GetProperty("championId").GetString());
        Assert.Equal(9, b5.GetProperty("stats").GetProperty("kills").GetInt32());
        Assert.Equal(12009, b5.GetProperty("stats").GetProperty("damageToChampions").GetInt32());
    }

    private static readonly string[] Positions = ["TOP", "JUNGLE", "MIDDLE", "BOTTOM", "UTILITY"];

    [Fact]
    public async Task Les_stats_de_cs_vision_objectifs_et_le_role_joue_sont_lues_et_les_stats_absentes_restent_vides()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        // A1..A5 envoient toutes les stats LCU ; B1..B5 seulement leurs kills.
        object StatsDe(int i) => i < 5
            ? new
            {
                kills = 3, deaths = 1, assists = 9,
                totalMinionsKilled = 180 + i, neutralMinionsKilled = 12,
                totalDamageDealtToChampions = 21000, totalDamageTaken = 15000, goldEarned = 11500,
                visionScore = 40 + i, wardsPlaced = 11, wardsKilled = 4, visionWardsBoughtInGame = 3,
                damageDealtToObjectives = 5200, turretKills = 2, dragonKills = 1, baronKills = 0,
                champLevel = 16, teamPosition = Positions[i]
            }
            : new { kills = 1 };
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_9000000002", "blue", "A", statsDe: StatsDe,
            donnees: new { gameDuration = 1800, gameMode = "CLASSIC", gameType = "CUSTOM_GAME", gameVersion = "14.20.1" })));

        var game = Assert.Single(await GamesDuMoisAsync(client));
        Assert.Equal("14.20.1", game.GetProperty("match").GetProperty("gameVersion").GetString());
        var participants = ParticipantsParJoueur(game);
        var a2 = participants[serie.TeamA[1]].GetProperty("stats");
        Assert.Equal(181, a2.GetProperty("minionsKilled").GetInt32());
        Assert.Equal(12, a2.GetProperty("neutralMinionsKilled").GetInt32());
        Assert.Equal(15000, a2.GetProperty("damageTaken").GetInt32());
        Assert.Equal(11500, a2.GetProperty("goldEarned").GetInt32());
        Assert.Equal(41, a2.GetProperty("visionScore").GetInt32());
        Assert.Equal(11, a2.GetProperty("wardsPlaced").GetInt32());
        Assert.Equal(4, a2.GetProperty("wardsKilled").GetInt32());
        Assert.Equal(3, a2.GetProperty("controlWardsBought").GetInt32());
        Assert.Equal(5200, a2.GetProperty("damageToObjectives").GetInt32());
        Assert.Equal(2, a2.GetProperty("turretKills").GetInt32());
        Assert.Equal(1, a2.GetProperty("dragonKills").GetInt32());
        Assert.Equal(0, a2.GetProperty("baronKills").GetInt32());
        Assert.Equal(16, a2.GetProperty("champLevel").GetInt32());
        Assert.Equal("Jungle", a2.GetProperty("position").GetString());
        Assert.Equal("Support", participants[serie.TeamA[4]].GetProperty("stats").GetProperty("position").GetString());

        var b1 = participants[serie.TeamB[0]].GetProperty("stats");
        Assert.Equal(1, b1.GetProperty("kills").GetInt32());
        Assert.Equal(JsonValueKind.Null, b1.GetProperty("deaths").ValueKind);
        Assert.Equal(JsonValueKind.Null, b1.GetProperty("visionScore").ValueKind);
        Assert.Equal(JsonValueKind.Null, b1.GetProperty("position").ValueKind);
    }

    [Fact]
    public async Task Une_game_saisie_a_la_main_figure_dans_l_historique_sans_donnees_de_match_ni_stats()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, serie.SeriesId, "A");

        var game = Assert.Single(await GamesDuMoisAsync(client));
        Assert.Equal(JsonValueKind.Null, game.GetProperty("riotMatchId").ValueKind);
        Assert.Equal(JsonValueKind.Null, game.GetProperty("match").ValueKind);
        var a1 = ParticipantsParJoueur(game)[serie.TeamA[0]];
        Assert.Equal(JsonValueKind.Null, a1.GetProperty("championId").ValueKind);
        Assert.Equal(JsonValueKind.Null, a1.GetProperty("stats").ValueKind);
    }

    [Fact]
    public async Task Les_rapports_de_plusieurs_companions_pour_la_meme_game_n_y_laissent_qu_une_fois_ses_donnees()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_9000000003", "blue", "A")));
        await PlayGameAsync(client, serie.SeriesId, teamASide: "red");

        // Deux autres Companions de la même partie envoient leur rapport, aux stats un peu différentes.
        await EnvoyerRapportAsync(code, Rapport("EUW1_9000000003", "blue", "A", emetteur: 3));
        await EnvoyerRapportAsync(code, Rapport("EUW1_9000000003", "blue", "A", emetteur: 7));

        var game = Assert.Single(await GamesDuMoisAsync(client));
        Assert.Equal("EUW1_9000000003", game.GetProperty("riotMatchId").GetString());
        Assert.Equal(1834, game.GetProperty("match").GetProperty("durationSeconds").GetInt32());
        var a2 = ParticipantsParJoueur(game)[serie.TeamA[1]];
        Assert.Equal(1, a2.GetProperty("stats").GetProperty("kills").GetInt32());
    }

    [Fact]
    public async Task Une_game_terminee_d_une_serie_interrompue_garde_ses_donnees_dans_l_historique()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_9000000004", "blue", "B")));
        await PlayGameAsync(client, serie.SeriesId, teamASide: "red");

        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));

        var now = Factory.Clock.GetUtcNow();
        var history = await ReadJsonAsync(await client.GetAsync($"/games/history/{now.Year}/{now.Month}", Ct));
        var lue = Assert.Single(history.GetProperty("games").EnumerateArray());
        Assert.Equal("Interrompue", lue.GetProperty("outcome").GetString());
        var game = Assert.Single(lue.GetProperty("games").EnumerateArray());
        Assert.Equal(gameId, game.GetProperty("id").GetInt32());
        Assert.Equal("EUW1_9000000004", game.GetProperty("riotMatchId").GetString());
        Assert.Equal(1834, game.GetProperty("match").GetProperty("durationSeconds").GetInt32());
        var b1 = ParticipantsParJoueur(game)[serie.TeamB[0]];
        Assert.Equal("Darius", b1.GetProperty("championId").GetString());
        Assert.Equal(12005, b1.GetProperty("stats").GetProperty("damageToChampions").GetInt32());
    }

    [Fact]
    public async Task Chaque_participant_a_sa_note_de_performance_sur_100_et_ses_composantes_selon_le_role_joue()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        // A3 joue mid, pile à la référence moyenne du rôle : KDA 3, KP 12/24, 7 CS/min, 700 dégâts/min,
        // 0,8 vision/min, 300 dégâts aux objectifs/min sur 30 minutes. L'Équipe A fait 24 kills.
        object StatsDe(int i) => i == 2
            ? new
            {
                kills = 5, deaths = 4, assists = 7, totalMinionsKilled = 190, neutralMinionsKilled = 20,
                totalDamageDealtToChampions = 21000, visionScore = 24, damageDealtToObjectives = 9000,
                teamPosition = "MIDDLE"
            }
            : new { kills = i < 5 ? (i == 4 ? 4 : 5) : 2, deaths = 3, assists = 6 };
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_9000000005", "blue", "A", statsDe: StatsDe,
            donnees: new { gameDuration = 1800, gameMode = "CLASSIC", gameType = "CUSTOM_GAME" })));

        var participants = ParticipantsParJoueur(Assert.Single(await GamesDuMoisAsync(client)));
        var a3 = participants[serie.TeamA[2]].GetProperty("performance");
        Assert.Equal(50, a3.GetProperty("note").GetInt32());
        Assert.Equal("Mid", a3.GetProperty("role").GetString());
        Assert.Equal("Partie", a3.GetProperty("sourceDuRole").GetString());
        var composantes = a3.GetProperty("composantes").EnumerateArray().ToList();
        Assert.Equal(
            ["Kda", "ParticipationAuxKills", "CsParMinute", "DegatsParMinute", "VisionParMinute", "ObjectifsParMinute"],
            composantes.Select(c => c.GetProperty("critere").GetString()));
        Assert.All(composantes, c => Assert.Equal(50, c.GetProperty("score").GetInt32()));
        var cs = composantes[2];
        Assert.Equal(7, cs.GetProperty("valeur").GetDouble());
        Assert.Equal(0.2, cs.GetProperty("poids").GetDouble());
    }

    [Fact]
    public async Task Sans_position_dans_la_partie_le_joueur_est_note_selon_son_role_principal_avec_les_seules_stats_transmises()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        // Le rapport par défaut ne donne que kills, morts, assists et dégâts ; A1 : 0/2/7, l'Équipe A fait 10 kills.
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_9000000006", "blue", "A",
            donnees: new { gameMode = "CLASSIC" })));

        var a1 = ParticipantsParJoueur(Assert.Single(await GamesDuMoisAsync(client)))[serie.TeamA[0]].GetProperty("performance");
        Assert.Equal("Mid", a1.GetProperty("role").GetString()); // rôle principal SoloQ (MIDDLE)
        Assert.Equal("RolePrincipal", a1.GetProperty("sourceDuRole").GetString());
        // Sans durée de game, seuls le KDA (3,5 : 58) et la KP (70 % : 100) sont notés, à poids égaux.
        Assert.Equal(["Kda", "ParticipationAuxKills"],
            a1.GetProperty("composantes").EnumerateArray().Select(c => c.GetProperty("critere").GetString()));
        Assert.Equal(79, a1.GetProperty("note").GetInt32());
    }

    [Fact]
    public async Task Une_game_saisie_a_la_main_n_a_pas_de_note_de_performance()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, serie.SeriesId, "A");

        var a1 = ParticipantsParJoueur(Assert.Single(await GamesDuMoisAsync(client)))[serie.TeamA[0]];
        Assert.Equal(JsonValueKind.Null, a1.GetProperty("performance").ValueKind);
    }
}
