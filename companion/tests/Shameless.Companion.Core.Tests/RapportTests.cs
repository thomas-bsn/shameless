using System.Text.Json;
using System.Text.Json.Nodes;

namespace Shameless.Companion.Core.Tests;

// Seam : ConstructeurRapport.Construire(eog, historique), fonction pure testée sur la capture réelle.
public class RapportTests
{
    private static RapportDeGame Construit(string? eog = null, string? historique = null) =>
        Assert.IsType<ResultatRapport.Construit>(
            ConstructeurRapport.Construire(eog ?? Fixtures.Eog(), historique ?? Fixtures.Historique())).Rapport;

    [Fact]
    public void La_custom_5v5_donne_un_Rapport_de_dix_joueurs_dont_cinq_par_Side()
    {
        var rapport = Construit();

        Assert.Equal(10, rapport.Joueurs.Count);
        Assert.Equal(5, rapport.Joueurs.Count(j => j.Side == "blue"));
        Assert.Equal(5, rapport.Joueurs.Count(j => j.Side == "red"));
    }

    [Fact]
    public void Le_riotMatchId_est_platformId_gameId()
    {
        Assert.Equal("EUW1_8019035742", Construit().RiotMatchId);
    }

    [Fact]
    public void Le_Side_vient_du_teamId_et_la_victoire_de_isWinningTeam()
    {
        var eog = JsonNode.Parse(Fixtures.Eog())!;
        var teams = eog["teams"]!.AsArray();
        // Dans la capture, l'équipe 100 (blue) a gagné : on inverse pour prouver que la victoire suit isWinningTeam.
        teams[0]!["isWinningTeam"] = false;
        teams[1]!["isWinningTeam"] = true;

        var rapport = Construit(eog.ToJsonString());

        Assert.All(rapport.Joueurs.Where(j => j.Side == "blue"), j => Assert.False(j.Victoire));
        Assert.All(rapport.Joueurs.Where(j => j.Side == "red"), j => Assert.True(j.Victoire));
    }

    [Fact]
    public void Le_champion_est_l_id_numerique_et_les_stats_sont_celles_du_joueur_sans_modification()
    {
        var eog = JsonNode.Parse(Fixtures.Eog())!;
        var premier = eog["teams"]![0]!["players"]![0]!;

        var joueur = Construit().Joueurs[0];

        Assert.Equal(premier["championId"]!.GetValue<int>(), joueur.ChampionId);
        Assert.Equal(premier["puuid"]!.GetValue<string>(), joueur.Puuid);
        Assert.True(JsonNode.DeepEquals(premier["stats"], JsonNode.Parse(joueur.Stats.GetRawText())));
        Assert.Equal(183, joueur.Stats.EnumerateObject().Count());
    }

    [Fact]
    public void Les_donnees_sont_une_liste_blanche_sans_jeton_ni_mot_de_passe_ni_XP()
    {
        var json = RapportJson.Serialiser(Construit());
        var donnees = JsonNode.Parse(json)!["donnees"]!.AsObject();

        Assert.Equal(8019035742, donnees["gameId"]!.GetValue<long>());
        Assert.Equal("CUSTOM_GAME", donnees["gameType"]!.GetValue<string>());
        Assert.Equal("EUW1", donnees["platformId"]!.GetValue<string>());
        Assert.Equal(11, donnees["mapId"]!.GetValue<int>());
        Assert.Equal(1507, donnees["gameLength"]!.GetValue<int>());

        foreach (var interdit in new[] { "mucJwtDto", "multiUserChatPassword", "multiUserChatId", "experienceEarned", "experienceTotal", "localPlayer", "teams" })
            Assert.False(donnees.ContainsKey(interdit), interdit);
        Assert.DoesNotContain("anon-jeton", json);
        Assert.DoesNotContain("multiUserChatPassword", json);
    }

    [Fact]
    public void Le_Rapport_serialise_au_format_du_contrat()
    {
        var json = JsonNode.Parse(RapportJson.Serialiser(Construit()))!.AsObject();

        Assert.Equal(["riotMatchId", "donnees", "joueurs"], json.Select(p => p.Key));
        var joueur = json["joueurs"]![0]!.AsObject();
        Assert.Equal(["puuid", "side", "championId", "victoire", "stats"], joueur.Select(p => p.Key));
    }

    private static string Ignore(string eog, string? historique = null) =>
        Assert.IsType<ResultatRapport.Ignore>(ConstructeurRapport.Construire(eog, historique ?? Fixtures.Historique())).Raison;

    private static string Modifie(Action<JsonNode> modif, string? json = null)
    {
        var noeud = JsonNode.Parse(json ?? Fixtures.Eog())!;
        modif(noeud);
        return noeud.ToJsonString();
    }

    [Fact]
    public void Une_partie_qui_n_est_pas_une_custom_est_ignoree()
    {
        var raison = Ignore(Modifie(e => e["gameType"] = "MATCHED_GAME"));
        Assert.Contains("MATCHED_GAME", raison);
    }

    [Fact]
    public void Une_custom_hors_de_la_Faille_est_ignoree()
    {
        var raison = Ignore(Fixtures.Eog(), Modifie(h => h["mapId"] = 12, Fixtures.Historique()));
        Assert.Contains("12", raison);
    }

    [Fact]
    public void Une_custom_qui_n_a_pas_dix_joueurs_est_ignoree()
    {
        var raison = Ignore(Modifie(e => e["teams"]![1]!["players"]!.AsArray().RemoveAt(0)));
        Assert.Contains("5v5", raison);
    }

    [Fact]
    public void Une_custom_a_une_seule_equipe_est_ignoree()
    {
        Ignore(Modifie(e => e["teams"]!.AsArray().RemoveAt(1)));
    }

    [Fact]
    public void La_raison_d_ignorer_se_connait_avant_de_lire_l_historique()
    {
        Assert.Null(ConstructeurRapport.RaisonDIgnorer(Fixtures.Eog()));
        Assert.NotNull(ConstructeurRapport.RaisonDIgnorer(Modifie(e => e["gameType"] = "PRACTICE_GAME")));
    }

    [Fact]
    public void Un_joueur_sans_PUUID_ne_fait_pas_planter_la_lecture_et_n_est_pas_invente()
    {
        var eog = Modifie(e =>
        {
            e["teams"]![0]!["players"]![0]!["puuid"] = "";
            e["teams"]![0]!["players"]![1]!.AsObject().Remove("puuid");
        });

        var rapport = Construit(eog);

        Assert.Equal(10, rapport.Joueurs.Count);
        Assert.Equal(2, rapport.JoueursSansPuuid.Count);
        Assert.All(rapport.JoueursSansPuuid, j => Assert.Null(j.Puuid));
        Assert.Contains("\"puuid\":null", RapportJson.Serialiser(rapport));
    }

    [Fact]
    public void Un_JSON_illisible_est_ignore_sans_exception()
    {
        Assert.Contains("illisible", Ignore("pas du json"));
    }
}
