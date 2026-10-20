using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// Ce que la page d'une game (<c>GET /games/{id}</c>) donne pour le panneau Impact d'un joueur : les sous-scores
/// Global / Vs adversaire / Objectifs / Équipe / Rôle avec lettre et lignes tirées de la note (ADR 0011), et le
/// face-à-face à six axes contre l'adversaire du même rôle (#200, spec #196). Game de 25 minutes, rôle Mid
/// (références du Mid de <c>ReferencesDeNote</c> et de <c>ReferencesDImpact</c>, à calibrer) ; les valeurs
/// attendues sont calculées à la main à partir de ces références.
/// </summary>
public class ImpactDuJoueurTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const int Duree = 1500;
    private static readonly string[] Positions = ["MIDDLE", "TOP", "JUNGLE", "BOTTOM", "UTILITY"];

    private static Dictionary<string, object> Stats(int i, Dictionary<string, object> joueur) => i switch
    {
        0 or 5 => joueur,
        < 5 => new() { ["kills"] = 0, ["deaths"] = 0, ["assists"] = 0, ["totalDamageDealtToChampions"] = 0, ["goldEarned"] = 1000, ["teamPosition"] = Positions[i] },
        _ => new() { ["kills"] = 0, ["deaths"] = 0, ["assists"] = 0, ["totalDamageDealtToChampions"] = 5000, ["goldEarned"] = 5000, ["teamPosition"] = Positions[i - 5] }
    };

    /// <summary>Le Mid de l'équipe A : 8/2/0, 200 CS, 25 000 dégâts, 16 000 or, vision 25, objectifs 10 000, 2 tourelles et 1 dragon, soins 2 000 + boucliers 1 000, 25 s de contrôle, 10 balises posées et 2 détruites.</summary>
    private static Dictionary<string, object> MidA() => new()
    {
        ["kills"] = 8, ["deaths"] = 2, ["assists"] = 0, ["totalMinionsKilled"] = 200, ["neutralMinionsKilled"] = 0,
        ["totalDamageDealtToChampions"] = 25000, ["goldEarned"] = 16000, ["visionScore"] = 25, ["wardsPlaced"] = 10, ["wardsKilled"] = 2,
        ["damageDealtToObjectives"] = 10000, ["turretKills"] = 2, ["dragonKills"] = 1, ["baronKills"] = 0,
        ["totalHeal"] = 2000, ["totalDamageShieldedOnTeammates"] = 1000, ["timeCCingOthers"] = 25, ["teamPosition"] = "MIDDLE"
    };

    /// <summary>Le Mid de l'équipe B : 0/10/0, 100 CS, 5 000 dégâts, 5 000 or, rien en vision ni en objectifs, 6 000 de soins, 30 s de contrôle.</summary>
    private static Dictionary<string, object> MidB() => new()
    {
        ["kills"] = 0, ["deaths"] = 10, ["assists"] = 0, ["totalMinionsKilled"] = 100, ["neutralMinionsKilled"] = 0,
        ["totalDamageDealtToChampions"] = 5000, ["goldEarned"] = 5000, ["visionScore"] = 0, ["wardsPlaced"] = 0, ["wardsKilled"] = 0,
        ["damageDealtToObjectives"] = 0, ["turretKills"] = 0, ["dragonKills"] = 0, ["baronKills"] = 0,
        ["totalHeal"] = 6000, ["totalDamageShieldedOnTeammates"] = 0, ["timeCCingOthers"] = 30, ["teamPosition"] = "MIDDLE"
    };

    private async Task<JsonElement> GameAsync(Dictionary<string, object> midA, Func<int, object>? equipeB = null)
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_7300000001", "blue", "A",
            statsDe: i => i < 5 ? Stats(i, midA) : equipeB?.Invoke(i) ?? Stats(i, MidB()),
            donnees: new { gameDuration = Duree, gameMode = "CLASSIC", gameType = "CUSTOM_GAME" })));
        return await ReadJsonAsync(await client.GetAsync($"/games/{gameId}", Ct));
    }

    private static JsonElement Joueur(JsonElement game, string equipe = "teamA") => game.GetProperty(equipe).EnumerateArray().First();

    private static JsonElement Impact(JsonElement game, string equipe = "teamA") => Joueur(game, equipe).GetProperty("impact");

    private static JsonElement Groupe(JsonElement impact, string nom) =>
        impact.GetProperty("sousScores").EnumerateArray().Single(g => g.GetProperty("groupe").GetString() == nom);

    private static string[] Criteres(JsonElement groupe) =>
        [.. groupe.GetProperty("lignes").EnumerateArray().Select(l => l.GetProperty("critere").GetString()!)];

    private static JsonElement Axe(JsonElement impact, string nom) =>
        impact.GetProperty("faceAFace").GetProperty("axes").EnumerateArray().Single(a => a.GetProperty("axe").GetString() == nom);

    private static (int Score, int Adversaire, int Ecart) Valeurs(JsonElement axe) =>
        (axe.GetProperty("score").GetInt32(), axe.GetProperty("scoreAdversaire").GetInt32(), axe.GetProperty("ecart").GetInt32());

    [Fact]
    public async Task Une_performance_au_sommet_de_toutes_les_references_est_S_plus_et_les_lignes_suivent_la_note()
    {
        var game = await GameAsync(MidA());

        var performance = Joueur(game).GetProperty("performance");
        Assert.Equal(100, performance.GetProperty("note").GetInt32());
        var global = Groupe(Impact(game), "Global");
        Assert.Equal("S+", global.GetProperty("lettre").GetString());
        Assert.Equal(100, global.GetProperty("score").GetInt32());
        // Une ligne par composante de la note, victoire comprise.
        Assert.Equal(performance.GetProperty("composantes").EnumerateArray().Select(c => c.GetProperty("critere").GetString()), Criteres(global));
        foreach (var ligne in global.GetProperty("lignes").EnumerateArray())
            Assert.Equal(100, ligne.GetProperty("score").GetInt32());
        Assert.Equal(100.0, global.GetProperty("lignes").EnumerateArray().Sum(l => l.GetProperty("points").GetDouble()), 0);
    }

    [Fact]
    public async Task Les_criteres_sont_repartis_dans_les_groupes_Vs_adversaire_Objectifs_Equipe_et_Role()
    {
        var impact = Impact(await GameAsync(MidA()));

        Assert.Equal(["Kda", "CsParMinute", "DegatsParMinute", "OrParMinute"], Criteres(Groupe(impact, "VsAdversaire")));
        Assert.Equal(["ObjectifsParMinute"], Criteres(Groupe(impact, "Objectifs")));
        Assert.Equal(["ParticipationAuxKills", "PartDesKills", "PartDesDegats", "PartDeLOr", "Victoire"], Criteres(Groupe(impact, "Equipe")));
        // Les trois critères les plus lourds du Mid (KDA 23, dégâts 20, objectifs 12), victoire exclue.
        Assert.Equal(["Kda", "DegatsParMinute", "ObjectifsParMinute"], Criteres(Groupe(impact, "Role")));
    }

    [Fact]
    public async Task Un_score_intermediaire_donne_sa_valeur_son_poids_et_ses_points()
    {
        var mid = MidA();
        mid["kills"] = 2; mid["deaths"] = 1; mid["assists"] = 0; // KDA 2,0 : 50 + 50 × (2 - 1,53) / (3,51 - 1,53) = 61,87
        var impact = Impact(await GameAsync(mid));

        var kda = Groupe(impact, "VsAdversaire").GetProperty("lignes").EnumerateArray().First();
        Assert.Equal("Kda", kda.GetProperty("critere").GetString());
        Assert.Equal(2.0, kda.GetProperty("valeur").GetDouble());
        Assert.Equal(62, kda.GetProperty("score").GetInt32());
        Assert.Equal(0.228, kda.GetProperty("poids").GetDouble()); // 23 / 101
        Assert.Equal(14.1, kda.GetProperty("points").GetDouble()); // 62 × 0,228
        // Groupe Rôle : (62 × 23 + 100 × 20 + 100 × 12) / 55 = 83,5 → 84, lettre S.
        var role = Groupe(impact, "Role");
        Assert.Equal(84, role.GetProperty("score").GetInt32());
        Assert.Equal("S", role.GetProperty("lettre").GetString());
    }

    [Fact]
    public async Task Une_stat_manquante_masque_sa_ligne_et_l_axe_qui_n_a_plus_rien_de_mesurable()
    {
        var mid = MidA();
        mid.Remove("visionScore"); mid.Remove("wardsPlaced"); mid.Remove("wardsKilled");
        var impact = Impact(await GameAsync(mid));

        Assert.DoesNotContain("VisionParMinute", Criteres(Groupe(impact, "Global")));
        var axes = impact.GetProperty("faceAFace").GetProperty("axes").EnumerateArray().Select(a => a.GetProperty("axe").GetString()).ToList();
        Assert.Equal(["Survie", "Utilite", "Combat", "Objectifs", "Economie"], axes);
    }

    [Fact]
    public async Task Le_face_a_face_donne_six_axes_leur_ecart_avec_l_adversaire_du_meme_role_et_les_axes_gagnes()
    {
        var game = await GameAsync(MidA());

        var impact = Impact(game);
        var face = impact.GetProperty("faceAFace");
        Assert.Equal(Joueur(game, "teamB").GetProperty("playerId").GetInt32(), face.GetProperty("adversaireId").GetInt32());
        Assert.Equal("Mid", face.GetProperty("role").GetString());
        Assert.Equal(["Survie", "Utilite", "Combat", "Objectifs", "Vision", "Economie"],
            face.GetProperty("axes").EnumerateArray().Select(a => a.GetProperty("axe").GetString()));

        // Survie : 0,08 mort/min ≤ 0,12 (excellent) = 100 ; l'adversaire, 0,4 ≥ 0,38 = 0.
        Assert.Equal((100, 0, 100), Valeurs(Axe(impact, "Survie")));
        // Utilité : (55 [120 soins et boucliers/min] + 72 [1,0 s de contrôle/min]) / 2 = 64 ; adversaire (85 + 83) / 2 = 84.
        Assert.Equal((64, 84, -20), Valeurs(Axe(impact, "Utilite")));
        // Combat : KDA, dégâts/min et part des dégâts à 100 ; adversaire (0 + 12,6 + 30) / 3 = 14.
        Assert.Equal((100, 14, 86), Valeurs(Axe(impact, "Combat")));
        Assert.Equal((100, 0, 100), Valeurs(Axe(impact, "Objectifs")));
        // Vision : (100 [1,0/min] + 46 [0,48 balise/min]) / 2 = 73.
        Assert.Equal((73, 0, 73), Valeurs(Axe(impact, "Vision")));
        Assert.Equal((100, 0, 100), Valeurs(Axe(impact, "Economie")));
        Assert.Equal(5, face.GetProperty("axesGagnes").GetInt32());
        Assert.Equal(1, face.GetProperty("axesPerdus").GetInt32());
    }

    [Fact]
    public async Task Sans_adversaire_unique_du_meme_role_il_n_y_a_pas_de_face_a_face_mais_les_sous_scores_restent()
    {
        // L'équipe B n'a que des supports : personne ne joue le Mid en face.
        var game = await GameAsync(MidA(), _ => new Dictionary<string, object> { ["kills"] = 1, ["deaths"] = 1, ["assists"] = 1, ["teamPosition"] = "UTILITY" });

        var impact = Impact(game);
        Assert.Equal(JsonValueKind.Null, impact.GetProperty("faceAFace").ValueKind);
        Assert.NotEmpty(Criteres(Groupe(impact, "Global")));
    }

    [Fact]
    public async Task Une_game_saisie_a_la_main_n_a_pas_d_impact()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");

        var game = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}", Ct));

        Assert.Equal(JsonValueKind.Null, Impact(game).ValueKind);
    }
}
