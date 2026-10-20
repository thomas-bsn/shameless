using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// Le Rapport enrichi du Companion (ADR 0021) : le rôle joué transmis par la partie (<c>teamPosition</c>)
/// est celui de la note (ADR 0011), les objets, sorts et objectifs d'équipe sont acceptés sans changer le contrat.
/// </summary>
public class RapportEnrichiTests(ApiFactory factory) : IntegrationTest(factory)
{
    /// <summary>Envoie un Rapport enrichi pour la game <paramref name="rang"/> de la Série (un Mid 9/4/5 en A1) et rend la game notée la plus récente de la fiche d'A1.</summary>
    private async Task<JsonElement> EnvoyerAsync(HttpClient client, CreatedSerie serie, string code, int rang, string position)
    {
        await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var reponse = await EnvoyerRapportAsync(code, Rapport($"EUW1_710000000{rang}", "blue", "A",
            statsDe: i => i == 0
                ? new
                {
                    kills = 9, deaths = 4, assists = 5, totalMinionsKilled = 200, totalDamageDealtToChampions = 22000,
                    teamPosition = position, items = new[] { 6655, 3020, 3157, 0, 0, 0, 3340 }, spell1Id = 4, spell2Id = 14
                }
                : new
                {
                    kills = 1, deaths = 4, assists = 2, totalMinionsKilled = 120, totalDamageDealtToChampions = 9000,
                    teamPosition = "UTILITY", items = new[] { 0, 0, 0, 0, 0, 0, 0 }, spell1Id = 4, spell2Id = 3
                },
            donnees: new
            {
                gameDuration = 1507, gameMode = "CLASSIC", gameType = "CUSTOM_GAME",
                teams = new[] { new { teamId = 100, dragonKills = 1, towerKills = 11, firstBlood = false } }
            }));
        await ReadJsonAsync(reponse);

        var fiche = await ReadJsonAsync(await client.GetAsync($"/players/{serie.TeamA[0]}/fiche", Ct));
        return fiche.GetProperty("games").EnumerateArray().First(g => g.GetProperty("performance").ValueKind == JsonValueKind.Object);
    }

    [Fact]
    public async Task Un_Rapport_enrichi_avec_teamPosition_MIDDLE_est_accepte_et_note_avec_le_role_Mid()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);

        var game = await EnvoyerAsync(client, serie, await CodeCompanionAsync(client, serie.SeriesId), 1, "MIDDLE");

        Assert.Equal("Mid", game.GetProperty("role").GetString());
        Assert.Equal("Mid", game.GetProperty("performance").GetProperty("role").GetString());
    }

    [Fact]
    public async Task Le_role_joue_change_la_note_une_meme_ligne_de_stats_ne_vaut_pas_pareil_en_Mid_et_en_Support()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        var mid = await EnvoyerAsync(client, serie, code, 1, "MIDDLE");
        var support = await EnvoyerAsync(client, serie, code, 2, "UTILITY");

        Assert.Equal("Mid", mid.GetProperty("performance").GetProperty("role").GetString());
        Assert.Equal("Support", support.GetProperty("performance").GetProperty("role").GetString());
        Assert.NotEqual(mid.GetProperty("performance").GetProperty("note").GetInt32(),
                        support.GetProperty("performance").GetProperty("note").GetInt32());
    }

    [Fact]
    public async Task La_Serie_donne_le_role_joue_de_chaque_joueur_dans_la_composition_de_sa_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        await EnvoyerAsync(client, serie, await CodeCompanionAsync(client, serie.SeriesId), 1, "MIDDLE");

        var lue = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));

        var roles = lue.GetProperty("games")[0].GetProperty("teamA").EnumerateArray()
            .ToDictionary(p => p.GetProperty("playerId").GetInt32(), p => p.GetProperty("role").GetString());
        Assert.Equal("Mid", roles[serie.TeamA[0]]);
        Assert.Equal("Support", roles[serie.TeamA[1]]);
    }
}
