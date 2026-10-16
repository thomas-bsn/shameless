using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// La fiche d'un Joueur regroupe ses stats de customs (games terminées, y compris celles d'une Série
/// interrompue), ses notes de performance, ses champions et rôles joués, son historique, son compte
/// Discord lié, son cost courant et ses rangs actuels.
/// </summary>
public class FicheJoueurTests(ApiFactory factory) : IntegrationTest(factory)
{
    private async Task<JsonElement> FicheAsync(HttpClient client, int playerId) =>
        await ReadJsonAsync(await client.GetAsync($"/players/{playerId}/fiche", Ct));

    [Fact]
    public async Task La_fiche_d_un_joueur_sans_game_donne_son_identite_son_cost_et_ses_rangs_actuels_sans_stats()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Riot.AddAccount("Sam#EUW", "EMERALD", "III", lp: 42, flexTier: "GOLD", flexDivision: "I", flexLp: 10);
        var created = await ReadJsonAsync(await client.PostAsJsonAsync("/players", new { prenom = "Sam", riotId = "Sam#EUW" }, Ct));
        var playerId = created.GetProperty("id").GetInt32();
        await ReadJsonAsync(await Factory.CreateAdminClient().PutAsJsonAsync($"/players/{playerId}/cost", new { cost = 4.5m }, Ct));

        var fiche = await FicheAsync(client, playerId);

        Assert.Equal(playerId, fiche.GetProperty("playerId").GetInt32());
        Assert.Equal("Sam", fiche.GetProperty("prenom").GetString());
        Assert.Equal("Sam#EUW", fiche.GetProperty("riotId").GetString());
        Assert.Equal(JsonValueKind.Null, fiche.GetProperty("discord").ValueKind);
        Assert.Equal(4.5m, fiche.GetProperty("cost").GetDecimal());
        var soloQ = fiche.GetProperty("soloQ");
        Assert.Equal("EMERALD", soloQ.GetProperty("tier").GetString());
        Assert.Equal(3, soloQ.GetProperty("division").GetInt32());
        Assert.Equal(42, soloQ.GetProperty("lp").GetInt32());
        var flex = fiche.GetProperty("flex");
        Assert.Equal("GOLD", flex.GetProperty("tier").GetString());
        Assert.Equal(1, flex.GetProperty("division").GetInt32());

        var stats = fiche.GetProperty("stats");
        Assert.Equal(0, stats.GetProperty("gamesJouees").GetInt32());
        Assert.Equal(JsonValueKind.Null, stats.GetProperty("winrate").ValueKind);
        Assert.Equal(JsonValueKind.Null, stats.GetProperty("kda").ValueKind);
        Assert.Equal(JsonValueKind.Null, stats.GetProperty("noteMoyenne").ValueKind);
        Assert.Empty(fiche.GetProperty("champions").EnumerateArray());
        Assert.Empty(fiche.GetProperty("roles").EnumerateArray());
        Assert.Empty(fiche.GetProperty("games").EnumerateArray());
    }

    [Fact]
    public async Task La_fiche_d_un_joueur_inconnu_est_introuvable()
    {
        var client = Factory.CreateSignedInClient();

        await AssertRefusAsync(await client.GetAsync("/players/4242/fiche", Ct), HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Série en BO5 de A1, arrêtée (interrompue) avec une game lancée mais pas terminée :
    /// G1 (Companion, victoire) A1 Garen top 4/2/6, 180 CS, 18 000 dégâts en 30 min, l'Équipe A fait 12 kills ;
    /// G2 (Companion, défaite) A1 Sett jungle 1/5/2, 120 CS, 9 000 dégâts en 20 min, l'Équipe A fait 5 kills ;
    /// G3 (saisie à la main, victoire) sans stats ; G4 lancée, jamais terminée.
    /// </summary>
    private async Task<(CreatedSerie Serie, int[] Games)> HistoriqueConnuAsync(HttpClient client)
    {
        var serie = await CreateSerieAsync(client, format: 5);
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        var g1 = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_7000000001", "blue", "A",
            statsDe: i => i == 0
                ? new { kills = 4, deaths = 2, assists = 6, totalMinionsKilled = 150, neutralMinionsKilled = 30, totalDamageDealtToChampions = 18000, teamPosition = "TOP" }
                : new { kills = i < 5 ? 2 : 1, deaths = 3, assists = 3 },
            donnees: new { gameDuration = 1800, gameMode = "CLASSIC" })));

        var g2 = await PlayGameAsync(client, serie.SeriesId, teamASide: "red");
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_7000000002", "red", "B",
            champions: ["Sett", .. ChampionsA[1..], .. ChampionsB],
            statsDe: i => i == 0
                ? new { kills = 1, deaths = 5, assists = 2, totalMinionsKilled = 120, neutralMinionsKilled = 0, totalDamageDealtToChampions = 9000, teamPosition = "JUNGLE" }
                : new { kills = i < 5 ? 1 : 4, deaths = 1, assists = 2 },
            donnees: new { gameDuration = 1200, gameMode = "CLASSIC" })));

        var g3 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayGameAsync(client, serie.SeriesId);
        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));
        return (serie, [g1, g2, g3]);
    }

    [Fact]
    public async Task Les_stats_de_la_fiche_comptent_les_games_terminees_d_une_serie_interrompue_et_omettent_les_donnees_absentes()
    {
        var client = Factory.CreateSignedInClient();
        var (serie, _) = await HistoriqueConnuAsync(client);

        var stats = (await FicheAsync(client, serie.TeamA[0])).GetProperty("stats");

        Assert.Equal(3, stats.GetProperty("gamesJouees").GetInt32());
        Assert.Equal(2, stats.GetProperty("victoires").GetInt32());
        Assert.Equal(1, stats.GetProperty("defaites").GetInt32());
        Assert.Equal(66.7, stats.GetProperty("winrate").GetDouble());
        // La game saisie à la main n'a pas de stats : seules G1 et G2 comptent.
        Assert.Equal(1.86, stats.GetProperty("kda").GetDouble()); // (4 + 6 + 1 + 2) / (2 + 5)
        Assert.Equal(2.5, stats.GetProperty("killsMoyens").GetDouble());
        Assert.Equal(3.5, stats.GetProperty("mortsMoyennes").GetDouble());
        Assert.Equal(4.0, stats.GetProperty("assistsMoyens").GetDouble());
        Assert.Equal(6.0, stats.GetProperty("csParMinute").GetDouble()); // 300 CS en 50 min
        Assert.Equal(76.5, stats.GetProperty("participationAuxKills").GetDouble()); // 13 / 17
        Assert.Equal(13500, stats.GetProperty("degatsMoyens").GetInt32());
        Assert.Equal(540, stats.GetProperty("degatsParMinute").GetInt32()); // 27 000 en 50 min
        Assert.Equal(2, stats.GetProperty("gamesNotees").GetInt32());
    }

    [Fact]
    public async Task La_fiche_donne_les_champions_et_les_roles_joues_avec_leurs_performances()
    {
        var client = Factory.CreateSignedInClient();
        var (serie, _) = await HistoriqueConnuAsync(client);

        var fiche = await FicheAsync(client, serie.TeamA[0]);

        var champions = fiche.GetProperty("champions").EnumerateArray().ToList();
        Assert.Equal(["Garen", "Sett"], champions.Select(c => c.GetProperty("championId").GetString()));
        var garen = champions[0].GetProperty("stats");
        Assert.Equal(1, garen.GetProperty("gamesJouees").GetInt32());
        Assert.Equal(100.0, garen.GetProperty("winrate").GetDouble());
        Assert.Equal(5.0, garen.GetProperty("kda").GetDouble());
        Assert.Equal(6.0, garen.GetProperty("csParMinute").GetDouble());
        Assert.Equal(0.0, champions[1].GetProperty("stats").GetProperty("winrate").GetDouble());

        var roles = fiche.GetProperty("roles").EnumerateArray().ToList();
        Assert.Equal(["Top", "Jungle"], roles.Select(r => r.GetProperty("role").GetString()));
        var jungle = roles[1].GetProperty("stats");
        Assert.Equal(1, jungle.GetProperty("gamesJouees").GetInt32());
        Assert.Equal(0.6, jungle.GetProperty("kda").GetDouble()); // (1 + 2) / 5
        Assert.Equal(450, jungle.GetProperty("degatsParMinute").GetInt32());
        Assert.Equal(jungle.GetProperty("noteMoyenne").GetDouble(),
            fiche.GetProperty("games")[1].GetProperty("performance").GetProperty("note").GetInt32());
    }

    [Fact]
    public async Task La_fiche_donne_l_historique_des_games_terminees_avec_la_note_de_performance_de_chacune()
    {
        var client = Factory.CreateSignedInClient();
        var (serie, gameIds) = await HistoriqueConnuAsync(client);

        var fiche = await FicheAsync(client, serie.TeamA[0]);

        var games = fiche.GetProperty("games").EnumerateArray().ToList();
        Assert.Equal(gameIds.Reverse(), games.Select(g => g.GetProperty("gameId").GetInt32()));
        Assert.All(games, g => Assert.Equal(serie.SeriesId, g.GetProperty("seriesId").GetInt32()));
        Assert.All(games, g => Assert.Equal("A", g.GetProperty("equipe").GetString()));
        Assert.Equal([true, false, true], games.Select(g => g.GetProperty("victoire").GetBoolean()));

        var (manuelle, g2, g1) = (games[0], games[1], games[2]);
        Assert.Equal(JsonValueKind.Null, manuelle.GetProperty("stats").ValueKind);
        Assert.Equal(JsonValueKind.Null, manuelle.GetProperty("performance").ValueKind);
        Assert.Equal(JsonValueKind.Null, manuelle.GetProperty("championId").ValueKind);
        Assert.Equal(JsonValueKind.Null, manuelle.GetProperty("dureeSecondes").ValueKind);

        Assert.Equal("Sett", g2.GetProperty("championId").GetString());
        Assert.Equal("Jungle", g2.GetProperty("role").GetString());
        Assert.Equal(1200, g2.GetProperty("dureeSecondes").GetInt32());
        Assert.Equal(1, g2.GetProperty("stats").GetProperty("kills").GetInt32());

        Assert.Equal("Garen", g1.GetProperty("championId").GetString());
        Assert.Equal("Top", g1.GetProperty("role").GetString());
        var note = g1.GetProperty("performance");
        Assert.Equal("Top", note.GetProperty("role").GetString());
        Assert.InRange(note.GetProperty("note").GetInt32(), 0, 100);
        Assert.NotEmpty(note.GetProperty("composantes").EnumerateArray());

        // La note moyenne de la fiche est celle des deux games notées.
        var notes = new[] { g1, g2 }.Select(g => g.GetProperty("performance").GetProperty("note").GetInt32()).ToList();
        Assert.Equal(Math.Round(notes.Average(), 1), fiche.GetProperty("stats").GetProperty("noteMoyenne").GetDouble());
    }

    [Fact]
    public async Task Les_notes_de_la_fiche_sont_celles_de_l_historique_des_games()
    {
        var client = Factory.CreateSignedInClient();
        var (serie, _) = await HistoriqueConnuAsync(client);
        var now = Factory.Clock.GetUtcNow();
        var history = await ReadJsonAsync(await client.GetAsync($"/games/history/{now.Year}/{now.Month}", Ct));
        var notesDeLHistorique = history.GetProperty("games").EnumerateArray()
            .SelectMany(s => s.GetProperty("games").EnumerateArray())
            .ToDictionary(
                g => g.GetProperty("id").GetInt32(),
                g => g.GetProperty("teamA").EnumerateArray()
                        .Single(p => p.GetProperty("playerId").GetInt32() == serie.TeamA[0])
                        .GetProperty("performance") is { ValueKind: JsonValueKind.Object } p
                    ? p.GetProperty("note").GetInt32() : (int?)null);

        var games = (await FicheAsync(client, serie.TeamA[0])).GetProperty("games").EnumerateArray();

        Assert.All(games, g => Assert.Equal(
            notesDeLHistorique[g.GetProperty("gameId").GetInt32()],
            g.GetProperty("performance") is { ValueKind: JsonValueKind.Object } p ? p.GetProperty("note").GetInt32() : null));
    }

    [Fact]
    public async Task La_fiche_donne_le_compte_discord_lie_au_joueur()
    {
        var client = Factory.CreateSignedInClient(discordId: "123456789012345678", username: "sam");
        var playerId = await CreatePlayerAsync(client, "Sam");
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/link", new { playerId }, Ct));

        var discord = (await FicheAsync(client, playerId)).GetProperty("discord");

        Assert.Equal("123456789012345678", discord.GetProperty("discordId").GetString());
        Assert.Equal("sam", discord.GetProperty("username").GetString());
    }
}
