using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// La correction des stats couvre toutes les stats utiles (ADR 0011, #173) : champion, voie, niveau, sorts,
/// séries, sbires, or, dégâts par type, vision, objectifs, sept objets, six runes et deux arbres, note.
/// Chaque lecture donne la valeur du Companion (<c>statsOrigine</c>) et la valeur corrigée (<c>stats</c>).
/// </summary>
public class CorrectionsElargiesTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const int Corrigee = 12;

    // Le nom du champ de l'API, sa clé dans le JSON du Companion et son emplacement dans `stats` (propriété, indice éventuel).
    private static readonly (string Champ, string CleBrute, string Propriete, int? Indice)[] Stats =
    [
        ("champLevel", "champLevel", "champLevel", null),
        ("spell1Id", "spell1Id", "spell1Id", null),
        ("spell2Id", "spell2Id", "spell2Id", null),
        ("largestKillingSpree", "largestKillingSpree", "largestKillingSpree", null),
        ("killingSprees", "killingSprees", "killingSprees", null),
        ("doubleKills", "doubleKills", "doubleKills", null),
        ("tripleKills", "tripleKills", "tripleKills", null),
        ("quadraKills", "quadraKills", "quadraKills", null),
        ("pentaKills", "pentaKills", "pentaKills", null),
        ("minionsKilled", "totalMinionsKilled", "minionsKilled", null),
        ("neutralMinionsKilled", "neutralMinionsKilled", "neutralMinionsKilled", null),
        ("goldEarned", "goldEarned", "goldEarned", null),
        ("goldSpent", "goldSpent", "goldSpent", null),
        ("physicalDamageToChampions", "physicalDamageDealtToChampions", "physicalDamageToChampions", null),
        ("magicDamageToChampions", "magicDamageDealtToChampions", "magicDamageToChampions", null),
        ("trueDamageToChampions", "trueDamageDealtToChampions", "trueDamageToChampions", null),
        ("damageToChampions", "totalDamageDealtToChampions", "damageToChampions", null),
        ("damageTaken", "totalDamageTaken", "damageTaken", null),
        ("totalHeal", "totalHeal", "totalHeal", null),
        ("damageShielded", "totalDamageShieldedOnTeammates", "damageShielded", null),
        ("timeCcingOthers", "timeCCingOthers", "timeCcingOthers", null),
        ("visionScore", "visionScore", "visionScore", null),
        ("wardsPlaced", "wardsPlaced", "wardsPlaced", null),
        ("wardsKilled", "wardsKilled", "wardsKilled", null),
        ("controlWardsBought", "visionWardsBoughtInGame", "controlWardsBought", null),
        ("damageToObjectives", "damageDealtToObjectives", "damageToObjectives", null),
        ("turretKills", "turretKills", "turretKills", null),
        ("dragonKills", "dragonKills", "dragonKills", null),
        ("baronKills", "baronKills", "baronKills", null),
        ("inhibitorKills", "inhibitorKills", "inhibitorKills", null),
        ("roleBoundItem", "roleBoundItem", "roleBoundItem", null),
        ("perkPrimaryStyle", "PERK_PRIMARY_STYLE", "perkPrimaryStyle", null),
        ("perkSubStyle", "PERK_SUB_STYLE", "perkSubStyle", null),
        .. Enumerable.Range(0, 7).Select(i => ($"item{i}", "", "items", (int?)i)),
        .. Enumerable.Range(0, 6).Select(i => ($"perk{i}", $"PERK{i}", "perks", (int?)i))
    ];

    public static TheoryData<string> Champs => [.. Stats.Select(s => s.Champ)];

    private static int Brute(int index) => 100 + index;

    private static object StatsBrutes(int _)
    {
        var stats = new Dictionary<string, object> { ["kills"] = 3, ["deaths"] = 2, ["assists"] = 7, ["totalDamageDealtToChampions"] = 12000 };
        for (var i = 0; i < Stats.Length; i++)
            if (Stats[i].CleBrute != "" && Stats[i].Propriete != "perks")
                stats[Stats[i].CleBrute] = Brute(i);
        stats["champLevel"] = 9;
        for (var i = 0; i < 6; i++) stats[$"PERK{i}"] = 8000 + i;
        stats["items"] = Enumerable.Range(0, 7).Select(i => 3000 + i).ToArray();
        return stats;
    }

    private static int Lire(JsonElement stats, (string Champ, string CleBrute, string Propriete, int? Indice) champ)
    {
        var valeur = stats.GetProperty(champ.Propriete);
        return (champ.Indice is { } i ? valeur[i] : valeur).GetInt32();
    }

    private int _rapports;

    private async Task<(HttpClient Client, int GameId, int PlayerId)> UneGameAsync()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport($"EUW1_8400{_rapports++:D5}", "blue", "A",
            statsDe: StatsBrutes, donnees: new { gameDuration = 1800, gameMode = "CLASSIC" })));
        return (client, gameId, serie.TeamA[0]);
    }

    private static Task<HttpResponseMessage> CorrigerAsync(HttpClient client, int gameId, int playerId, object corps) =>
        client.PutAsJsonAsync($"/games/{gameId}/players/{playerId}/corrections", corps, Ct);

    private async Task<JsonElement> JoueurAsync(HttpClient client, int gameId, int playerId)
    {
        var game = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}", Ct));
        return game.GetProperty("teamA").EnumerateArray().Concat(game.GetProperty("teamB").EnumerateArray())
            .Single(p => p.GetProperty("playerId").GetInt32() == playerId);
    }

    [Theory]
    [MemberData(nameof(Champs))]
    public async Task Chaque_stat_est_corrigeable_et_retablissable_avec_sa_valeur_d_origine(string nomDuChamp)
    {
        var champ = Stats.Single(s => s.Champ == nomDuChamp);
        var index = Array.IndexOf(Stats, champ);
        var attendueBrute = champ.Propriete switch
        {
            "items" => 3000 + champ.Indice!.Value,
            "perks" => 8000 + champ.Indice!.Value,
            "champLevel" => 9,
            _ => Brute(index)
        };
        var (client, gameId, a1) = await UneGameAsync();
        var admin = Factory.CreateAdminClient();

        var joueur = await JoueurAsync(client, gameId, a1);
        Assert.Equal(attendueBrute, Lire(joueur.GetProperty("stats"), champ));

        await ReadJsonAsync(await CorrigerAsync(admin, gameId, a1, new Dictionary<string, int> { [nomDuChamp] = Corrigee }));
        joueur = await JoueurAsync(client, gameId, a1);
        Assert.Equal(Corrigee, Lire(joueur.GetProperty("stats"), champ));
        Assert.Equal(attendueBrute, Lire(joueur.GetProperty("statsOrigine"), champ));
        Assert.Equal([nomDuChamp], joueur.GetProperty("champsCorriges").EnumerateArray().Select(c => c.GetString()));

        var retabli = await admin.DeleteAsync($"/games/{gameId}/players/{a1}/corrections/{nomDuChamp}", Ct);
        await ReadJsonAsync(retabli);
        joueur = await JoueurAsync(client, gameId, a1);
        Assert.Equal(attendueBrute, Lire(joueur.GetProperty("stats"), champ));
        Assert.Equal(0, joueur.GetProperty("champsCorriges").GetArrayLength());
    }

    [Fact]
    public async Task Corriger_deux_objets_et_une_rune_ne_touche_pas_aux_autres_emplacements()
    {
        var (client, gameId, a1) = await UneGameAsync();

        await ReadJsonAsync(await CorrigerAsync(Factory.CreateAdminClient(), gameId, a1, new { item0 = 3157, item6 = 0, perk3 = 8112 }));

        var stats = (await JoueurAsync(client, gameId, a1)).GetProperty("stats");
        Assert.Equal([3157, 3001, 3002, 3003, 3004, 3005, 0], stats.GetProperty("items").EnumerateArray().Select(i => i.GetInt32()));
        Assert.Equal(8112, stats.GetProperty("perks")[3].GetInt32());
        Assert.Equal(8000, stats.GetProperty("perks")[0].GetInt32());
    }

    [Fact]
    public async Task Le_champion_la_voie_et_la_note_gardent_leur_valeur_d_origine_a_cote_de_la_corrigee()
    {
        var (client, gameId, a1) = await UneGameAsync();
        var avant = await JoueurAsync(client, gameId, a1);
        var noteAvant = avant.GetProperty("performance").GetProperty("note").GetInt32();

        await ReadJsonAsync(await CorrigerAsync(Factory.CreateAdminClient(), gameId, a1,
            new { champion = "Darius", position = "Top", note = 97 }));

        var joueur = await JoueurAsync(client, gameId, a1);
        Assert.Equal("Darius", joueur.GetProperty("championId").GetString());
        Assert.Equal("Garen", joueur.GetProperty("championOrigine").GetString());
        Assert.Equal(97, joueur.GetProperty("performance").GetProperty("note").GetInt32());
        Assert.NotEqual(97, noteAvant);
        Assert.Equal(noteAvant, joueur.GetProperty("performance").GetProperty("noteOrigine").GetInt32());
    }

    [Fact]
    public async Task Corriger_les_stats_d_un_joueur_recalcule_son_cost()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient();
        Factory.Clock.SetUtcNow(new DateTimeOffset(2026, 3, 10, 20, 0, 0, TimeSpan.Zero));
        var serie = await CreateSerieAsync(client, format: 5);
        var games = new List<int>();
        foreach (var gagnante in new[] { "A", "B", "A", "B", "A" })
        {
            games.Add(await PlayGameAsync(client, serie.SeriesId, teamASide: "blue"));
            var code = await CodeCompanionAsync(client, serie.SeriesId);
            await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport($"EUW1_8500{_rapports++:D5}", "blue", gagnante,
                statsDe: StatsBrutes, donnees: new { gameDuration = 1800, gameMode = "CLASSIC" })));
        }
        var a1 = serie.TeamA[0];
        async Task<decimal> Cost() =>
            (await ReadJsonAsync(await client.GetAsync($"/players/{a1}/cost", Ct))).GetProperty("cost").GetDecimal();
        var avant = await Cost();

        foreach (var game in games)
            await ReadJsonAsync(await CorrigerAsync(admin, game, a1, new { kills = 25, deaths = 0, assists = 20, visionScore = 120, cs = 400 }));

        Assert.NotEqual(avant, await Cost());
    }

    [Theory]
    [InlineData("champLevel", 0, "champLevel")]
    [InlineData("champLevel", 19, "champLevel")]
    [InlineData("item3", -1, "item3")]
    [InlineData("perk2", -5, "perk2")]
    [InlineData("pentaKills", 100, "pentaKills")]
    [InlineData("spell1Id", -1, "spell1Id")]
    public async Task Une_valeur_invalide_est_refusee_avec_un_message_qui_nomme_le_champ(string champ, int valeur, string attendu)
    {
        var (client, gameId, a1) = await UneGameAsync();

        var reponse = await CorrigerAsync(Factory.CreateAdminClient(), gameId, a1, new Dictionary<string, int> { [champ] = valeur });

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        var detail = (await reponse.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("detail").GetString();
        Assert.Contains(attendu, detail);
        var game = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}", Ct));
        Assert.Equal(JsonValueKind.Null, game.GetProperty("modifieeParAdminLe").ValueKind);
    }
}
