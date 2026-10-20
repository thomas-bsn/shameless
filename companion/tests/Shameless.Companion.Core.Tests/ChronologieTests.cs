using System.Text.Json;
using System.Text.Json.Nodes;

namespace Shameless.Companion.Core.Tests;

internal sealed class PartieEnCoursSimulee : IPartieEnCours
{
    public Queue<string?> Reponses { get; } = new();
    public bool Leve { get; set; }

    public Task<string?> LireAsync(CancellationToken ct)
    {
        if (Leve) throw new HttpRequestException("connexion refusée");
        return Task.FromResult(Reponses.Count > 0 ? Reponses.Dequeue() : null);
    }
}

// Seams : LiveClient.Lire (fonction pure) et EchantillonneurDePartie, sur des fixtures de la Live Client Data API.
public class ChronologieTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly PartieEnCoursSimulee _partie = new();
    private readonly JournalEnMemoire _journal = new();

    private EchantillonneurDePartie Echantillonneur() => new(_partie, _journal);

    /// <summary>Le bloc de fin de game de la capture, avec un PUUID distinct par joueur (les bots de la capture en partagent un).</summary>
    private static string EogAvecPuuidsDistincts(int gameLength = 1507)
    {
        var eog = JsonNode.Parse(Fixtures.Eog())!;
        eog["gameLength"] = gameLength;
        foreach (var joueur in eog["teams"]!.AsArray().SelectMany(t => t!["players"]!.AsArray()))
            joueur!["puuid"] = "puuid-" + joueur["riotIdGameName"]!.GetValue<string>();
        return eog.ToJsonString();
    }

    private async Task<ChronologieDuRapport?> Chronologie(string eog, params string[] fixtures)
    {
        var e = Echantillonneur();
        foreach (var f in fixtures)
        {
            _partie.Reponses.Enqueue(Fixtures.LiveClient(f));
            await e.EchantillonnerAsync(Ct);
        }
        return e.Prendre(eog);
    }

    [Fact]
    public void La_lecture_donne_le_temps_et_les_joueurs_avec_leurs_valeurs_brutes()
    {
        var instantane = LiveClient.Lire(Fixtures.LiveClient("allgamedata-milieu.json"))!;

        Assert.Equal(645, instantane.Temps);
        Assert.Equal(10, instantane.Joueurs.Count);
        var sam = instantane.Joueurs.Single(j => j.RiotId == "Joueur1#TAG1");
        Assert.Equal(3, sam.Valeurs!.Kills);
        Assert.Equal(1, sam.Valeurs.Deaths);
        Assert.Equal(2, sam.Valeurs.Assists);
        Assert.Equal(85, sam.Valeurs.Cs);
        Assert.Equal(9, sam.Valeurs.Level);
        Assert.Equal(12, sam.Valeurs.Vision);
        Assert.Equal(450 + 1100 + 2 * 50, sam.Valeurs.ItemsValue);
        Assert.False(sam.Valeurs.Dead);
        Assert.True(instantane.Joueurs.Single(j => j.RiotId == "Joueur7#TAG2").Valeurs!.Dead);
    }

    [Fact]
    public void Les_evenements_d_objectifs_portent_le_side_de_l_equipe_qui_les_prend()
    {
        var evenements = LiveClient.Lire(Fixtures.LiveClient("allgamedata-fin.json"))!.Evenements;

        Assert.Equal(
            [(412, "turret", "blue"), (601, "dragon", "blue"), (640, "herald", "red"),
             (1320, "inhibitor", "blue"), (1405, "baron", "red"), (1440, "turret", "red")],
            evenements.Select(e => (e.T, e.Type, e.Side)));
    }

    [Theory]
    [InlineData("allgamedata-format-inattendu.json")]
    [InlineData("")]
    [InlineData("pas du json")]
    [InlineData("[]")]
    public void Un_format_inattendu_ne_donne_rien(string nom)
    {
        var json = nom.EndsWith(".json") ? Fixtures.LiveClient(nom) : nom;
        Assert.Null(LiveClient.Lire(json));
    }

    [Fact]
    public void Un_objet_sans_prix_laisse_le_joueur_hors_du_point_sans_inventer_de_valeur()
    {
        var json = JsonNode.Parse(Fixtures.LiveClient("allgamedata-milieu.json"))!;
        json["allPlayers"]![0]!["items"]![1]!.AsObject().Remove("price");

        var instantane = LiveClient.Lire(json.ToJsonString())!;

        Assert.Null(instantane.Joueurs.Single(j => j.RiotId == "Joueur1#TAG1").Valeurs);
        Assert.NotNull(instantane.Joueurs.Single(j => j.RiotId == "Joueur2#TAG1").Valeurs);
    }

    [Fact]
    public async Task Les_points_sont_rattaches_au_PUUID_du_Rapport_et_les_evenements_gardes()
    {
        var chronologie = await Chronologie(EogAvecPuuidsDistincts(),
            "allgamedata-debut.json", "allgamedata-milieu.json", "allgamedata-fin.json");

        Assert.NotNull(chronologie);
        Assert.Equal([95, 645, 1498], chronologie.Points.Select(p => p.T));
        Assert.All(chronologie.Points, p => Assert.Equal(10, p.Joueurs.Count));
        var sam = chronologie.Points[2].Joueurs.Single(j => j.Puuid == "puuid-Joueur1");
        Assert.Equal((11, 4, 6, 231, 16, 31), (sam.Kills, sam.Deaths, sam.Assists, sam.Cs, sam.Level, sam.Vision));
        Assert.Equal(1100 + 3000 + 3000 + 3 * 50, sam.ItemsValue);
        Assert.Equal(6, chronologie.Evenements.Count);
    }

    [Fact]
    public async Task Les_PUUID_partages_ou_absents_ne_sont_pas_rattaches()
    {
        // La capture : neuf bots partagent le même PUUID, seul le joueur humain a le sien.
        var chronologie = await Chronologie(Fixtures.Eog(), "allgamedata-milieu.json", "allgamedata-fin.json");

        Assert.NotNull(chronologie);
        Assert.All(chronologie.Points, p => Assert.Equal("anon-puuid-0001", Assert.Single(p.Joueurs).Puuid));
    }

    [Fact]
    public async Task Un_point_dont_la_date_ne_progresse_pas_est_ignore()
    {
        var chronologie = await Chronologie(EogAvecPuuidsDistincts(),
            "allgamedata-milieu.json", "allgamedata-milieu.json", "allgamedata-fin.json");

        Assert.Equal([645, 1498], chronologie!.Points.Select(p => p.T));
    }

    [Fact]
    public async Task Une_nouvelle_partie_repart_de_zero()
    {
        // fin (24 min) puis debut (1 min) : le temps recule, c'est une autre partie.
        var chronologie = await Chronologie(EogAvecPuuidsDistincts(gameLength: 100), "allgamedata-fin.json", "allgamedata-debut.json");

        Assert.Equal([95], chronologie!.Points.Select(p => p.T));
    }

    [Fact]
    public async Task L_API_locale_indisponible_ne_donne_pas_de_chronologie_ni_d_erreur()
    {
        var e = Echantillonneur();
        _partie.Leve = true;
        await e.EchantillonnerAsync(Ct);
        _partie.Leve = false;
        _partie.Reponses.Enqueue(null);
        await e.EchantillonnerAsync(Ct);

        Assert.Null(e.Prendre(EogAvecPuuidsDistincts()));
    }

    [Fact]
    public async Task Un_format_inattendu_ne_donne_pas_de_chronologie_et_le_journal_le_dit_une_fois()
    {
        var e = Echantillonneur();
        for (var i = 0; i < 3; i++)
        {
            _partie.Reponses.Enqueue(Fixtures.LiveClient("allgamedata-format-inattendu.json"));
            await e.EchantillonnerAsync(Ct);
        }

        Assert.Null(e.Prendre(EogAvecPuuidsDistincts()));
        Assert.Single(_journal.Lignes, l => l.Contains("format inattendu"));
    }

    [Fact]
    public async Task Un_enregistrement_qui_ne_finit_pas_avec_la_partie_du_Rapport_est_refuse()
    {
        // La partie du Rapport dure 1507 s ; l'échantillonnage s'arrête à 645 s (une autre partie, ou l'API perdue).
        Assert.Null(await Chronologie(EogAvecPuuidsDistincts(), "allgamedata-milieu.json"));
    }

    [Fact]
    public async Task Un_enregistrement_ne_sert_qu_une_fois()
    {
        var e = Echantillonneur();
        _partie.Reponses.Enqueue(Fixtures.LiveClient("allgamedata-fin.json"));
        await e.EchantillonnerAsync(Ct);

        Assert.NotNull(e.Prendre(EogAvecPuuidsDistincts()));
        Assert.Null(e.Prendre(EogAvecPuuidsDistincts()));
    }

    [Fact]
    public async Task Les_joueurs_inconnus_du_Rapport_sont_laisses_de_cote()
    {
        var eog = JsonNode.Parse(EogAvecPuuidsDistincts())!;
        foreach (var joueur in eog["teams"]!.AsArray().SelectMany(t => t!["players"]!.AsArray()))
            if (joueur!["riotIdGameName"]!.GetValue<string>() != "Joueur1")
            {
                joueur["riotIdGameName"] = "Autre" + joueur["riotIdGameName"]!.GetValue<string>();
                joueur["summonerName"] = joueur["riotIdGameName"]!.GetValue<string>();
            }

        var chronologie = await Chronologie(eog.ToJsonString(), "allgamedata-fin.json");

        Assert.Equal("puuid-Joueur1", Assert.Single(Assert.Single(chronologie!.Points).Joueurs).Puuid);
    }

    [Fact]
    public async Task Au_plus_deux_mille_points()
    {
        var e = Echantillonneur();
        for (var t = 1; t <= 2100; t++)
        {
            var json = JsonNode.Parse(Fixtures.LiveClient("allgamedata-fin.json"))!;
            json["gameData"]!["gameTime"] = t;
            _partie.Reponses.Enqueue(json.ToJsonString());
            await e.EchantillonnerAsync(Ct);
        }

        var chronologie = e.Prendre(EogAvecPuuidsDistincts(gameLength: 2000));
        Assert.Equal(2000, chronologie!.Points.Count);
    }

    [Fact]
    public async Task Le_Rapport_serialise_la_chronologie_au_format_du_contrat()
    {
        var chronologie = await Chronologie(EogAvecPuuidsDistincts(), "allgamedata-fin.json");
        var rapport = Assert.IsType<ResultatRapport.Construit>(
            ConstructeurRapport.Construire(EogAvecPuuidsDistincts(), Fixtures.Historique())).Rapport with { Chronologie = chronologie };

        var json = JsonNode.Parse(RapportJson.Serialiser(rapport))!["chronologie"]!;

        var point = json["points"]![0]!;
        Assert.Equal(1498, point["t"]!.GetValue<int>());
        var joueur = point["joueurs"]!.AsArray().Single(j => j!["puuid"]!.GetValue<string>() == "puuid-Joueur1")!;
        Assert.Equal(
            ["puuid", "kills", "deaths", "assists", "cs", "level", "vision", "itemsValue", "dead"],
            joueur.AsObject().Select(p => p.Key));
        Assert.Equal(
            ["t", "type", "side"], json["evenements"]![0]!.AsObject().Select(p => p.Key));
    }

    [Fact]
    public void Sans_chronologie_le_Rapport_part_avec_chronologie_nulle()
    {
        var rapport = Assert.IsType<ResultatRapport.Construit>(
            ConstructeurRapport.Construire(Fixtures.Eog(), Fixtures.Historique())).Rapport;

        Assert.Null(JsonNode.Parse(RapportJson.Serialiser(rapport))!["chronologie"]);
    }

    private async Task<RapportDeGame> RapportApresLaPartie(Func<EchantillonneurDePartie, Task> pendantLaPartie)
    {
        var client = new ClientLolSimule();
        var echantillonneur = Echantillonneur();
        await pendantLaPartie(echantillonneur);
        client.Reponses["/lol-end-of-game/v1/eog-stats-block"] = [new(200, EogAvecPuuidsDistincts())];
        client.Reponses["/lol-match-history/v1/games/8019035742"] = [new(200, Fixtures.Historique())];
        var surveillance = new SurveillanceFinDeGame(client, _journal, new OptionsSurveillance(1, TimeSpan.Zero), echantillonneur);
        var rapports = new List<RapportDeGame>();
        surveillance.RapportConstruit += rapports.Add;
        client.Evenements.Writer.TryWrite(new EvenementClientLol.Message(
            "/lol-end-of-game/v1/eog-stats-block", "Update", JsonDocument.Parse("""{"gameId":8019035742}""").RootElement.Clone()));
        client.Evenements.Writer.Complete();
        await surveillance.ExecuterAsync(Ct);
        return Assert.Single(rapports);
    }

    [Fact]
    public async Task La_fin_de_game_joint_la_chronologie_echantillonnee_au_Rapport()
    {
        var rapport = await RapportApresLaPartie(async e =>
        {
            foreach (var f in new[] { "allgamedata-debut.json", "allgamedata-milieu.json", "allgamedata-fin.json" })
            {
                _partie.Reponses.Enqueue(Fixtures.LiveClient(f));
                await e.EchantillonnerAsync(Ct);
            }
        });

        Assert.Equal(3, rapport.Chronologie!.Points.Count);
        Assert.Equal(10, rapport.Joueurs.Count);
        Assert.Contains(_journal.Lignes, l => l.Contains("Chronologie jointe"));
    }

    [Fact]
    public async Task Sans_API_locale_le_Rapport_part_sans_chronologie_et_sans_erreur()
    {
        var rapport = await RapportApresLaPartie(async e =>
        {
            _partie.Leve = true;
            await e.EchantillonnerAsync(Ct);
        });

        Assert.Null(rapport.Chronologie);
        Assert.DoesNotContain(_journal.Lignes, l => l.Contains("abandonnée"));
    }
}
