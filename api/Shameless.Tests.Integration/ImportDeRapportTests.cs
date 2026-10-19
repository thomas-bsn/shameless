using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// L'Import d'un Rapport (ADR 0020) : un Administrateur dépose le Fichier de Rapport sur une game
/// terminée. Même validation que le Rapport ; participants, Sides et vainqueur identiques à la game,
/// sinon refus complet avec la liste des écarts. Jamais de changement de vainqueur ni de composition ;
/// les costs sont recalculés.
/// </summary>
public class ImportDeRapportTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static readonly DateTimeOffset Mars = new(2026, 3, 10, 20, 0, 0, TimeSpan.Zero);

    private static Task<HttpResponseMessage> ImporterAsync(HttpClient client, int gameId, object rapport) =>
        client.PostAsJsonAsync($"/games/{gameId}/import-rapport", rapport, Ct);

    private async Task<JsonElement> JoueurAsync(HttpClient client, int gameId, int playerId)
    {
        var game = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}", Ct));
        return game.GetProperty("teamA").EnumerateArray().Concat(game.GetProperty("teamB").EnumerateArray())
            .Single(p => p.GetProperty("playerId").GetInt32() == playerId);
    }

    private async Task<decimal> CostAsync(HttpClient client, int playerId) =>
        (await ReadJsonAsync(await client.GetAsync($"/players/{playerId}/cost", Ct))).GetProperty("cost").GetDecimal();

    [Fact]
    public async Task L_import_est_reserve_aux_administrateurs()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");
        var rapport = Rapport("EUW1_9100000001", "blue", "A");

        Assert.Equal(HttpStatusCode.Forbidden, (await ImporterAsync(client, gameId, rapport)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ImporterAsync(Factory.CreateAnonymousClient(), gameId, rapport)).StatusCode);
    }

    [Fact]
    public async Task Un_fichier_conforme_donne_a_la_game_ses_stats_ses_champions_et_son_identifiant_riot_et_recalcule_les_costs()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient();
        Factory.Clock.SetUtcNow(Mars);
        var serie = await CreateSerieAsync(client, format: 5);
        var games = new List<int>();
        foreach (var gagnante in new[] { "A", "B", "A", "B", "A" })
            games.Add(await PlayAndWinAsync(client, serie.SeriesId, gagnante));
        var a1 = serie.TeamA[0];
        var costAvant = await CostAsync(client, a1);

        var parametres = (await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct))).GetProperty("games")
            .EnumerateArray().Select(g => (Side: g.GetProperty("teamASide").GetString()!, Gagnante: g.GetProperty("winner").GetString()!)).ToList();
        object FichierDe(int rang) => Rapport($"EUW1_910000000{2 + rang}", parametres[rang].Side, parametres[rang].Gagnante,
            statsDe: i => i == 0
                ? new { kills = 25, deaths = 0, assists = 12, totalMinionsKilled = 300, totalDamageDealtToChampions = 60000 }
                : new { kills = 0, deaths = 8, assists = 0, totalMinionsKilled = 50, totalDamageDealtToChampions = 5000 });
        var reponse = await ImporterAsync(admin, games[0], FichierDe(0));
        await ReadJsonAsync(await ImporterAsync(admin, games[1], FichierDe(1)));
        for (var rang = 2; rang < games.Count; rang++)
            await ReadJsonAsync(await ImporterAsync(admin, games[rang], FichierDe(rang)));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var lue = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        var game = lue.GetProperty("games")[0];
        Assert.Equal("EUW1_9100000002", game.GetProperty("riotMatchId").GetString());
        Assert.Equal("A", game.GetProperty("winner").GetString());
        var champions = game.GetProperty("championsJoues").EnumerateArray()
            .ToDictionary(c => c.GetProperty("playerId").GetInt32(), c => c.GetProperty("championId").GetString());
        Assert.Equal("Garen", champions[a1]);
        Assert.Equal("Nami", champions[serie.TeamB[4]]);
        Assert.Equal(25, (await JoueurAsync(client, games[0], a1)).GetProperty("stats").GetProperty("kills").GetInt32());
        Assert.NotEqual(costAvant, await CostAsync(client, a1));
    }

    [Fact]
    public async Task Un_fichier_dont_les_participants_les_sides_ou_le_vainqueur_different_est_refuse_en_entier_avec_la_liste_des_ecarts()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient();
        await CreatePlayerAsync(client, "X1");
        var serie = await CreateSerieAsync(client);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A", teamASide: "blue");

        var autreJoueur = await ImporterAsync(admin, gameId,
            Rapport("EUW1_9100000003", "blue", "A", equipeA: ["A1", "A2", "A3", "A4", "X1"]));
        var sidesInverses = await ImporterAsync(admin, gameId, Rapport("EUW1_9100000004", "red", "A"));
        var autreVainqueur = await ImporterAsync(admin, gameId, Rapport("EUW1_9100000005", "blue", "B"));

        Assert.Equal(HttpStatusCode.Conflict, autreJoueur.StatusCode);
        var ecarts = (await autreJoueur.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("ecarts")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(2, ecarts.Count);
        Assert.Contains(ecarts, e => e!.Contains("A5"));
        Assert.Contains(ecarts, e => e!.Contains("puuid-X1#EUW"));

        Assert.Equal(HttpStatusCode.Conflict, sidesInverses.StatusCode);
        Assert.Contains("Side", (await sidesInverses.Content.ReadFromJsonAsync<JsonElement>(Ct))
            .GetProperty("ecarts")[0].GetString());

        Assert.Equal(HttpStatusCode.Conflict, autreVainqueur.StatusCode);
        Assert.Contains("vainqueur", (await autreVainqueur.Content.ReadFromJsonAsync<JsonElement>(Ct))
            .GetProperty("ecarts")[0].GetString());

        // Refus complet : rien n'a changé, ni vainqueur, ni composition, ni données Riot.
        var game = (await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct))).GetProperty("games")[0];
        Assert.Equal(JsonValueKind.Null, game.GetProperty("riotMatchId").ValueKind);
        Assert.Equal("A", game.GetProperty("winner").GetString());
        Assert.Equal("blue", game.GetProperty("teamASide").GetString());
        Assert.Empty(game.GetProperty("championsJoues").EnumerateArray());
    }

    [Fact]
    public async Task Un_fichier_mal_forme_est_refuse_en_400_et_une_game_en_attente_ou_inconnue_ne_prend_pas_d_import()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient();
        var serie = await CreateSerieAsync(client);
        var enAttente = await PlayGameAsync(client, serie.SeriesId, "blue");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await ImporterAsync(admin, enAttente, new { riotMatchId = "EUW1_9100000006", joueurs = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ImporterAsync(admin, enAttente, Rapport("EUW1_9100000007", "blue", "A"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ImporterAsync(admin, 987654, Rapport("EUW1_9100000008", "blue", "A"))).StatusCode);
    }

    [Fact]
    public async Task Un_champion_inconnu_du_catalogue_laisse_la_game_inchangee()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");
        var fichier = JsonSerializer.SerializeToNode(Rapport("EUW1_9100000009", "blue", "A"))!;
        fichier["joueurs"]![2]!["championId"] = 9999;

        var reponse = await ImporterAsync(Factory.CreateAdminClient(), gameId, fichier);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, reponse.StatusCode);
        var game = (await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct))).GetProperty("games")[0];
        Assert.Equal(JsonValueKind.Null, game.GetProperty("riotMatchId").ValueKind);
    }
}
