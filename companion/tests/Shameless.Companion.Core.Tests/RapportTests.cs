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
        // 183 stats du client + teamPosition, items, spell1Id, spell2Id (ADR 0021).
        Assert.Equal(187, joueur.Stats.EnumerateObject().Count());
        var sansAjouts = JsonNode.Parse(joueur.Stats.GetRawText())!.AsObject();
        foreach (var cle in new[] { "teamPosition", "items", "spell1Id", "spell2Id" }) sansAjouts.Remove(cle);
        Assert.True(JsonNode.DeepEquals(premier["stats"], sansAjouts));
    }

    [Fact]
    public void Chaque_joueur_porte_le_role_joue_les_sept_objets_et_les_deux_sorts()
    {
        var eog = JsonNode.Parse(Fixtures.Eog())!;
        var attendus = eog["teams"]!.AsArray().SelectMany(t => t!["players"]!.AsArray()).ToList();

        var rapport = Construit();

        Assert.Equal(10, rapport.Joueurs.Count);
        for (var i = 0; i < 10; i++)
        {
            var stats = JsonNode.Parse(rapport.Joueurs[i].Stats.GetRawText())!;
            Assert.Equal(attendus[i]!["detectedTeamPosition"]!.GetValue<string>(), stats["teamPosition"]!.GetValue<string>());
            Assert.Equal(7, stats["items"]!.AsArray().Count);
            Assert.True(JsonNode.DeepEquals(attendus[i]!["items"], stats["items"]));
            Assert.Equal(attendus[i]!["spell1Id"]!.GetValue<int>(), stats["spell1Id"]!.GetValue<int>());
            Assert.Equal(attendus[i]!["spell2Id"]!.GetValue<int>(), stats["spell2Id"]!.GetValue<int>());
        }
        // Zed est Mid dans la capture : le rôle vient du client, pas d'une déduction.
        Assert.Equal("MIDDLE", JsonNode.Parse(rapport.Joueurs[2].Stats.GetRawText())!["teamPosition"]!.GetValue<string>());
    }

    [Fact]
    public void Les_emplacements_d_objets_vides_sont_gardes_et_un_joueur_sans_role_ni_objets_reste_rapporte()
    {
        var eog = Modifie(e =>
        {
            var j = e["teams"]![0]!["players"]![0]!.AsObject();
            j["items"] = new JsonArray(3068, 0, 0, 0, 0, 0, 3340);
            j.Remove("detectedTeamPosition");
            j.Remove("spell2Id");
            e["teams"]![0]!["players"]![1]!.AsObject().Remove("items");
        });

        var joueurs = Construit(eog).Joueurs;
        var premier = JsonNode.Parse(joueurs[0].Stats.GetRawText())!;
        var second = JsonNode.Parse(joueurs[1].Stats.GetRawText())!;

        Assert.Equal([3068, 0, 0, 0, 0, 0, 3340], premier["items"]!.AsArray().Select(n => n!.GetValue<int>()));
        Assert.Null(premier["teamPosition"]);
        Assert.Null(premier["spell2Id"]);
        Assert.Null(second["items"]);
    }

    [Fact]
    public void Les_donnees_portent_les_objectifs_des_deux_equipes_lus_dans_l_historique()
    {
        var donnees = JsonNode.Parse(RapportJson.Serialiser(Construit()))!["donnees"]!.AsObject();
        var equipes = donnees["teams"]!.AsArray();

        Assert.Equal(2, equipes.Count);
        var bleue = equipes.Single(e => e!["teamId"]!.GetValue<int>() == 100)!;
        var rouge = equipes.Single(e => e!["teamId"]!.GetValue<int>() == 200)!;
        Assert.Equal(1, bleue["dragonKills"]!.GetValue<int>());
        Assert.Equal(0, bleue["baronKills"]!.GetValue<int>());
        Assert.Equal(1, bleue["riftHeraldKills"]!.GetValue<int>());
        Assert.Equal(3, bleue["hordeKills"]!.GetValue<int>());
        Assert.Equal(11, bleue["towerKills"]!.GetValue<int>());
        Assert.Equal(2, bleue["inhibitorKills"]!.GetValue<int>());
        Assert.False(bleue["firstBlood"]!.GetValue<bool>());
        Assert.True(bleue["firstTower"]!.GetValue<bool>());
        Assert.True(bleue["firstInhibitor"]!.GetValue<bool>());
        Assert.Equal(2, rouge["towerKills"]!.GetValue<int>());
        Assert.True(rouge["firstBlood"]!.GetValue<bool>());
        // Liste blanche : rien d'autre que le teamId et les neuf objectifs (pas de bans, pas de win).
        Assert.Equal(10, bleue.AsObject().Count);
    }

    [Fact]
    public void Un_historique_sans_equipes_donne_un_Rapport_sans_objectifs()
    {
        var historique = Modifie(h => h.AsObject().Remove("teams"), Fixtures.Historique());

        var donnees = JsonNode.Parse(RapportJson.Serialiser(Construit(historique: historique)))!["donnees"]!.AsObject();

        Assert.False(donnees.ContainsKey("teams"));
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

        foreach (var interdit in new[] { "mucJwtDto", "multiUserChatPassword", "multiUserChatId", "experienceEarned", "experienceTotal", "localPlayer" })
            Assert.False(donnees.ContainsKey(interdit), interdit);
        // Aucun secret nulle part dans le Rapport, stats et objectifs compris.
        foreach (var secret in new[] { "mucJwtDto", "multiUserChatId", "experienceEarned", "experienceTotal", "previousXpTotal", "nextLevelXp" })
            Assert.DoesNotContain(secret, json);
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
