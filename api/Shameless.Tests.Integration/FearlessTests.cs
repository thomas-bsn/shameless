using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Fearless : la Draft de champions d'une game montre les champions réellement joués dans les games
/// précédentes de la Série, d'après les rapports du Companion. Elle n'empêche rien.
/// </summary>
public class FearlessTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static readonly string[] ChampionsA = ["Garen", "LeeSin", "Ahri", "Jinx", "Thresh"];
    private static readonly string[] ChampionsB = ["Darius", "Zed", "Orianna", "Ezreal", "Nami"];
    private static readonly string[] Joues = [.. ChampionsA, .. ChampionsB];

    private sealed record Preparee(CreatedSerie Serie, string Code, string LienA, string LienB, string LienSpectateur);

    /// <summary>Une Série avec ses deux capitaines, ses liens envoyés et son code Companion.</summary>
    private async Task<Preparee> PreparerAsync(HttpClient client, bool fearless)
    {
        var serie = await CreateSerieAsync(client, format: 5, fearless: fearless);
        await ReadJsonAsync(await client.PutAsJsonAsync($"/series/{serie.SeriesId}/capitaines/A",
            new { designation = "Manuel", playerId = serie.TeamA[0] }, Ct));
        await ReadJsonAsync(await client.PutAsJsonAsync($"/series/{serie.SeriesId}/capitaines/B",
            new { designation = "Manuel", playerId = serie.TeamB[0] }, Ct));
        var liens = await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/liens/envoyes", null, Ct));
        var code = (await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}/code-companion", Ct)))
            .GetProperty("code").GetString()!;
        return new Preparee(serie, code,
            liens.GetProperty("lienCapitaineA").GetString()!,
            liens.GetProperty("lienCapitaineB").GetString()!,
            liens.GetProperty("lienSpectateur").GetString()!);
    }

    /// <summary>Lance une game (Équipe A en blue) et la termine par un rapport où chacun joue les champions donnés.</summary>
    private async Task<int> JouerAvecRapportAsync(HttpClient client, Preparee preparee, string riotMatchId, string[] championsA, string[] championsB)
    {
        var gameId = await PlayGameAsync(client, preparee.Serie.SeriesId, "blue");
        var joueurs = championsA.Select((c, i) => (Puuid: $"puuid-A{i + 1}#EUW", Side: "blue", Champion: c, Victoire: true))
            .Concat(championsB.Select((c, i) => (Puuid: $"puuid-B{i + 1}#EUW", Side: "red", Champion: c, Victoire: false)))
            .Select(j => new { puuid = j.Puuid, side = j.Side, championId = j.Champion, victoire = j.Victoire, stats = new { kills = 1 } })
            .ToArray();
        var rapport = new { riotMatchId, donnees = new { gameMode = "CLASSIC" }, joueurs };
        await ReadJsonAsync(await Factory.CreateAnonymousClient().PostAsJsonAsync(
            $"/companion/{Uri.EscapeDataString(preparee.Code)}/rapports", rapport, Ct));
        return gameId;
    }

    private async Task<JsonElement> OuvrirAsync(HttpClient client, int seriesId) =>
        await ReadJsonAsync(await client.PostAsync($"/series/{seriesId}/draft-champions", null, Ct));

    private async Task<JsonElement> LireParLienAsync(string lien) =>
        await ReadJsonAsync(await Factory.CreateAnonymousClient().GetAsync($"/liens/{Uri.EscapeDataString(lien)}/draft-champions", Ct));

    private static string[] ChampionsDejaJoues(JsonElement draft) =>
        draft.GetProperty("championsDejaJoues").EnumerateArray().Select(c => c.GetString()!).ToArray();

    [Fact]
    public async Task En_fearless_les_trois_vues_de_la_draft_exposent_les_champions_joues_dans_les_games_precedentes()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client, fearless: true);
        await JouerAvecRapportAsync(client, preparee, "EUW1_8000000001", ChampionsA, ChampionsB);
        var suivante = await PlayGameAsync(client, preparee.Serie.SeriesId, "blue");

        var ouverte = await OuvrirAsync(client, preparee.Serie.SeriesId);
        var organisateur = await ReadJsonAsync(await client.GetAsync($"/games/{suivante}/draft-champions", Ct));
        var capitaine = await LireParLienAsync(preparee.LienA);
        var spectateur = await LireParLienAsync(preparee.LienSpectateur);

        Assert.Equal(suivante, ouverte.GetProperty("gameId").GetInt32());
        foreach (var draft in new[] { ouverte, organisateur, capitaine, spectateur })
            Assert.Equal(Joues.Order(), ChampionsDejaJoues(draft).Order());
    }

    [Fact]
    public async Task Les_champions_deja_joues_restent_bannissables_et_pickables()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client, fearless: true);
        await JouerAvecRapportAsync(client, preparee, "EUW1_8000000011", ChampionsA, ChampionsB);
        await PlayGameAsync(client, preparee.Serie.SeriesId, "blue");
        await OuvrirAsync(client, preparee.Serie.SeriesId);

        // Équipe A en blue : tour 1 ban blue, tour 2 ban red, ..., tour 7 pick blue.
        string[] tours = ["Garen", "Darius", "LeeSin", "Zed", "Ahri", "Orianna", "Jinx"];
        for (var tour = 1; tour <= tours.Length; tour++)
        {
            var lien = tour % 2 == 1 ? preparee.LienA : preparee.LienB;
            var reponse = await Factory.CreateAnonymousClient().PostAsJsonAsync(
                $"/liens/{Uri.EscapeDataString(lien)}/draft-champions/actions", new { tour, championId = tours[tour - 1] }, Ct);
            Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        }

        var draft = await LireParLienAsync(preparee.LienSpectateur);
        Assert.Equal(tours, draft.GetProperty("actions").EnumerateArray().Select(a => a.GetProperty("championId").GetString()));
        Assert.Equal(Joues.Order(), ChampionsDejaJoues(draft).Order());
    }

    [Fact]
    public async Task Seules_les_games_precedentes_de_la_serie_comptent_sans_doublon()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client, fearless: true);
        string[] autresA = ["Garen", "Irelia", "Janna", "Karma", "Lulu"];
        string[] autresB = ["Darius", "Malphite", "Pyke", "Riven", "Sona"];
        var premiere = await JouerAvecRapportAsync(client, preparee, "EUW1_8000000021", ChampionsA, ChampionsB);
        await PlayGameAsync(client, preparee.Serie.SeriesId, "blue");
        var deuxieme = (await OuvrirAsync(client, preparee.Serie.SeriesId)).GetProperty("gameId").GetInt32();
        await Factory.CreateAnonymousClient().PostAsJsonAsync($"/companion/{Uri.EscapeDataString(preparee.Code)}/rapports", new
        {
            riotMatchId = "EUW1_8000000022",
            donnees = new { gameMode = "CLASSIC" },
            joueurs = autresA.Select((c, i) => new { puuid = $"puuid-A{i + 1}#EUW", side = "blue", championId = c, victoire = false, stats = new { kills = 1 } })
                .Concat(autresB.Select((c, i) => new { puuid = $"puuid-B{i + 1}#EUW", side = "red", championId = c, victoire = true, stats = new { kills = 1 } }))
                .ToArray()
        }, Ct);
        var troisieme = await PlayGameAsync(client, preparee.Serie.SeriesId, "blue");
        await OuvrirAsync(client, preparee.Serie.SeriesId);

        var draftDeuxieme = await ReadJsonAsync(await client.GetAsync($"/games/{deuxieme}/draft-champions", Ct));
        var draftTroisieme = await ReadJsonAsync(await client.GetAsync($"/games/{troisieme}/draft-champions", Ct));

        Assert.NotEqual(premiere, deuxieme);
        Assert.Equal(Joues.Order(), ChampionsDejaJoues(draftDeuxieme).Order());
        Assert.Equal(
            ["Ahri", "Darius", "Ezreal", "Garen", "Irelia", "Janna", "Jinx", "Karma", "LeeSin", "Lulu",
             "Malphite", "Nami", "Orianna", "Pyke", "Riven", "Sona", "Thresh", "Zed"],
            ChampionsDejaJoues(draftTroisieme).Order());
    }

    [Fact]
    public async Task Une_game_terminee_sans_rapport_n_ajoute_aucun_champion()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client, fearless: true);
        await PlayAndWinAsync(client, preparee.Serie.SeriesId, "A");
        await PlayGameAsync(client, preparee.Serie.SeriesId, "blue");

        var draft = await OuvrirAsync(client, preparee.Serie.SeriesId);

        Assert.Empty(ChampionsDejaJoues(draft));
    }

    [Fact]
    public async Task Sans_fearless_la_liste_des_champions_deja_joues_est_absente()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client, fearless: false);
        await JouerAvecRapportAsync(client, preparee, "EUW1_8000000031", ChampionsA, ChampionsB);
        var suivante = await PlayGameAsync(client, preparee.Serie.SeriesId, "blue");

        var ouverte = await OuvrirAsync(client, preparee.Serie.SeriesId);
        var organisateur = await ReadJsonAsync(await client.GetAsync($"/games/{suivante}/draft-champions", Ct));
        var capitaine = await LireParLienAsync(preparee.LienB);
        var spectateur = await LireParLienAsync(preparee.LienSpectateur);

        foreach (var draft in new[] { ouverte, organisateur, capitaine, spectateur })
            Assert.Equal(JsonValueKind.Null, draft.GetProperty("championsDejaJoues").ValueKind);
    }
}
