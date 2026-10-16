using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>Les classements de l'Instance : customs 5v5 (winrate) et SoloQ (rangs actuels, par file classée).</summary>
public class ClassementsTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Le_classement_5v5_est_ordonne_par_winrate_et_donne_les_games_jouees()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayAndWinAsync(client, serie.SeriesId, "B");
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        var equipeC = await CreatePlayersAsync(client, "C1", "C2", "C3", "C4", "C5");
        var revanche = await ReadJsonAsync(await client.PostAsJsonAsync("/series",
            new { teamA = equipeC, teamB = serie.TeamB, format = 1, fearless = false, alternateSides = false }, Ct));
        await PlayAndWinAsync(client, revanche.GetProperty("seriesId").GetInt32(), "B");

        var lignes = await Classement5v5Async(client);

        // A : 2 V / 1 D (67 %), B : 2 V / 2 D (50 %, mais plus de games), C : 0 V / 1 D.
        Assert.Equal(
            ["A1", "A2", "A3", "A4", "A5", "B1", "B2", "B3", "B4", "B5", "C1", "C2", "C3", "C4", "C5"],
            lignes.Select(l => l.GetProperty("prenom").GetString()));
        var a1 = lignes[0];
        Assert.Equal(serie.TeamA[0], a1.GetProperty("playerId").GetInt32());
        Assert.Equal((3, 2, 1), Bilan(a1));
        Assert.Equal(66.7, a1.GetProperty("winrate").GetDouble());
        Assert.Equal((4, 2, 2), Bilan(lignes[5]));
        Assert.Equal(50.0, lignes[5].GetProperty("winrate").GetDouble());
        Assert.Equal((1, 0, 1), Bilan(lignes[10]));
        Assert.Equal(0.0, lignes[10].GetProperty("winrate").GetDouble());
    }

    [Fact]
    public async Task Les_games_terminees_d_une_serie_interrompue_comptent_au_classement_5v5()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5);
        await PlayAndWinAsync(client, serie.SeriesId, "B");
        await PlayAndWinAsync(client, serie.SeriesId, "B");
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayGameAsync(client, serie.SeriesId);
        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));

        var lignes = await Classement5v5Async(client);

        Assert.Equal("B1", lignes[0].GetProperty("prenom").GetString());
        Assert.Equal((3, 2, 1), Bilan(lignes[0]));
        Assert.Equal("A1", lignes[5].GetProperty("prenom").GetString());
        Assert.Equal((3, 1, 2), Bilan(lignes[5]));
        Assert.Equal(33.3, lignes[5].GetProperty("winrate").GetDouble());
    }

    [Fact]
    public async Task Un_joueur_sans_game_terminee_reste_au_classement_5v5_sans_winrate_apres_les_autres()
    {
        var client = Factory.CreateSignedInClient();
        await CreatePlayerAsync(client, "Aaron");
        var serie = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, serie.SeriesId, "B");

        var lignes = await Classement5v5Async(client);

        Assert.Equal(11, lignes.Count);
        var aaron = lignes[^1];
        Assert.Equal("Aaron", aaron.GetProperty("prenom").GetString());
        Assert.Equal((0, 0, 0), Bilan(aaron));
        Assert.Equal(JsonValueKind.Null, aaron.GetProperty("winrate").ValueKind);
        Assert.Equal(0.0, lignes[^2].GetProperty("winrate").GetDouble());
    }

    [Fact]
    public async Task Le_classement_soloq_classe_les_rangs_actuels_par_file_classee()
    {
        var client = Factory.CreateSignedInClient();
        await CreateClasseAsync(client, "Zed", solo: ("DIAMOND", "IV", 10), flex: null);
        await CreateClasseAsync(client, "Yuumi", solo: ("GOLD", "I", 50), flex: ("PLATINUM", "II", 20));
        await CreateClasseAsync(client, "Xin", solo: ("GOLD", "I", 80), flex: ("GOLD", "III", 0));
        await CreateClasseAsync(client, "Wukong", solo: null, flex: ("MASTER", "I", 120));
        await CreateClasseAsync(client, "Vex", solo: null, flex: null);

        var classement = await ReadJsonAsync(await client.GetAsync("/classements/soloq", Ct));

        var solo = classement.GetProperty("solo").EnumerateArray().ToList();
        Assert.Equal(["Zed", "Xin", "Yuumi", "Vex", "Wukong"], solo.Select(l => l.GetProperty("prenom").GetString()));
        Assert.Equal(("DIAMOND", 4, 10), Rang(solo[0]));
        Assert.Equal(("GOLD", 1, 80), Rang(solo[1]));
        Assert.Equal((null, null, null), Rang(solo[3]));
        var flex = classement.GetProperty("flex").EnumerateArray().ToList();
        Assert.Equal(["Wukong", "Yuumi", "Xin", "Vex", "Zed"], flex.Select(l => l.GetProperty("prenom").GetString()));
        Assert.Equal(("MASTER", null, 120), Rang(flex[0]));
        Assert.Equal(("PLATINUM", 2, 20), Rang(flex[1]));
        Assert.Equal((null, null, null), Rang(flex[4]));
    }

    [Fact]
    public async Task La_fiche_d_un_joueur_donne_son_rang_actuel_dans_chaque_file_classee()
    {
        var client = Factory.CreateSignedInClient();
        var yuumi = await CreateClasseAsync(client, "Yuumi", solo: ("GOLD", "I", 50), flex: ("PLATINUM", "II", 20));
        var vex = await CreateClasseAsync(client, "Vex", solo: ("SILVER", "III", 5), flex: null);

        var fiche = await ReadJsonAsync(await client.GetAsync($"/players/{yuumi}", Ct));
        var sansFlex = await ReadJsonAsync(await client.GetAsync($"/players/{vex}", Ct));

        Assert.Equal(("GOLD", 1, 50), (fiche.GetProperty("rankTier").GetString(), fiche.GetProperty("rankDivision").GetInt32(), fiche.GetProperty("lp").GetInt32()));
        Assert.Equal(("PLATINUM", 2, 20), (fiche.GetProperty("flexRankTier").GetString(), fiche.GetProperty("flexRankDivision").GetInt32(), fiche.GetProperty("flexLp").GetInt32()));
        Assert.Equal(JsonValueKind.Null, sansFlex.GetProperty("flexRankTier").ValueKind);
        Assert.Equal(JsonValueKind.Null, sansFlex.GetProperty("flexLp").ValueKind);
    }

    /// <summary>Enregistre un Joueur dont le compte Riot a ces rangs (null : pas classé dans la file).</summary>
    private async Task<int> CreateClasseAsync(HttpClient client, string prenom,
        (string Tier, string Division, int Lp)? solo, (string Tier, string Division, int Lp)? flex)
    {
        var riotId = $"{prenom}#EUW";
        Factory.Riot.AddAccount(riotId, solo?.Tier, solo?.Division, solo?.Lp ?? 0,
            flexTier: flex?.Tier, flexDivision: flex?.Division, flexLp: flex?.Lp ?? 0);
        return (await ReadJsonAsync(await client.PostAsJsonAsync("/players", new { prenom, riotId }, Ct)))
            .GetProperty("id").GetInt32();
    }

    private static (string? Tier, int? Division, int? Lp) Rang(JsonElement ligne) =>
        (ligne.GetProperty("tier").GetString(),
            ligne.GetProperty("division").ValueKind == JsonValueKind.Null ? null : ligne.GetProperty("division").GetInt32(),
            ligne.GetProperty("lp").ValueKind == JsonValueKind.Null ? null : ligne.GetProperty("lp").GetInt32());

    private async Task<List<JsonElement>> Classement5v5Async(HttpClient client) =>
        (await ReadJsonAsync(await client.GetAsync("/classements/5v5", Ct)))
            .GetProperty("joueurs").EnumerateArray().ToList();

    private static (int Games, int Victoires, int Defaites) Bilan(JsonElement ligne) =>
        (ligne.GetProperty("gamesJouees").GetInt32(), ligne.GetProperty("victoires").GetInt32(),
            ligne.GetProperty("defaites").GetInt32());
}
