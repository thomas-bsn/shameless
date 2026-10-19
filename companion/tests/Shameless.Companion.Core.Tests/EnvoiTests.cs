using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Shameless.Companion.Core.Tests;

// Seam : EnvoiRapports. API simulée au niveau HTTP, stockage en mémoire (ou disque temporaire pour le relancement).
public class EnvoiTests
{
    private const string Url = "https://shameless.example";

    private static RapportDeGame UnRapport(string id = "EUW1_100") =>
        new(id, new JsonObject { ["gameId"] = 100 }, []);

    private static string Traite(string statut) =>
        $$"""{"seriesId":1,"gameId":1,"statut":"{{statut}}","winner":null,"teamAWins":0,"teamBWins":0,"outcome":"InProgress"}""";

    private sealed class Banc
    {
        public ApiSimulee Api { get; } = new();
        public StockageEnMemoire Stockage { get; } = new();
        public List<ResultatEnvoi> Resultats { get; } = [];
        public List<TimeSpan> Attentes { get; } = [];
        public int AnnuleApresAttentes { get; set; } = 50;
        public Action<int>? AuxAttentes { get; set; }
        public IStockageIdentifiants Ids { get; set; } = new IdentifiantsFixes(new IdentifiantsCompanion(Url + "/", "CODE"));

        public EnvoiRapports Envoi(IStockageEnvoi? stockage = null)
        {
            var cts = new CancellationTokenSource();
            Cts = cts;
            var envoi = new EnvoiRapports(stockage ?? Stockage, Ids, Api.Client(), new JournalEnMemoire(),
                new OptionsEnvoi(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(60), 3),
                (delai, ct) =>
                {
                    Attentes.Add(delai);
                    AuxAttentes?.Invoke(Attentes.Count);
                    if (Attentes.Count >= AnnuleApresAttentes) cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                });
            envoi.Resultat += Resultats.Add;
            return envoi;
        }

        public CancellationTokenSource Cts { get; private set; } = new();
    }

    [Theory]
    [InlineData("Accepte", IssueEnvoi.Accepte)]
    [InlineData("EnRevue", IssueEnvoi.EnRevue)]
    [InlineData("Rejete", IssueEnvoi.Rejete)]
    public async Task Un_Rapport_est_poste_avec_le_Code_puis_sort_de_la_file_selon_la_reponse(string statut, IssueEnvoi attendu)
    {
        var banc = new Banc();
        banc.Api.Repond("POST", "/companion/CODE/rapports", HttpStatusCode.OK, Traite(statut));
        var envoi = banc.Envoi();

        await envoi.SoumettreAsync(UnRapport(), banc.Cts.Token);

        var requete = Assert.Single(banc.Api.Requetes, r => r.Methode == "POST");
        Assert.Equal(Url + "/companion/CODE/rapports", requete.Url);
        Assert.Equal(attendu, Assert.Single(banc.Resultats).Issue);
        Assert.Empty(banc.Stockage.LireFile());
    }

    [Fact]
    public async Task Le_corps_envoye_est_exactement_le_Fichier_de_Rapport()
    {
        var banc = new Banc();
        banc.Api.Repond("POST", "/companion/CODE/rapports", HttpStatusCode.OK, Traite("Accepte"));

        await banc.Envoi().SoumettreAsync(UnRapport("EUW1_7"), banc.Cts.Token);

        var fichier = Assert.Single(banc.Stockage.Fichiers);
        Assert.Equal("rapport-EUW1_7.json", fichier.Key);
        Assert.Equal(fichier.Value, Assert.Single(banc.Api.Requetes, r => r.Methode == "POST").Corps);
        Assert.Equal(RapportJson.Serialiser(UnRapport("EUW1_7")), fichier.Value);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task Un_refus_definitif_sort_de_la_file_et_garde_le_fichier(HttpStatusCode statut)
    {
        var banc = new Banc();
        banc.Api.Repond("POST", "/companion/CODE/rapports", statut, """{"title":"Série terminée"}""");

        await banc.Envoi().SoumettreAsync(UnRapport(), banc.Cts.Token);

        var resultat = Assert.Single(banc.Resultats);
        Assert.Equal(IssueEnvoi.Refuse, resultat.Issue);
        Assert.True(resultat.ProcedureManuelle);
        Assert.Contains("Série terminée", resultat.Message);
        Assert.Empty(banc.Stockage.LireFile());
        Assert.Contains("rapport-EUW1_100.json", banc.Stockage.Fichiers.Keys);
        Assert.Single(banc.Api.Requetes, r => r.Methode == "POST");
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Une_erreur_serveur_est_reessayee_avec_un_backoff_exponentiel_plafonne(HttpStatusCode statut)
    {
        var banc = new Banc { AnnuleApresAttentes = 7 };
        banc.Api.Repond("POST", "/companion/CODE/rapports", statut, "");

        await banc.Envoi().SoumettreAsync(UnRapport(), banc.Cts.Token);

        Assert.Equal([5, 10, 20, 40, 60, 60, 60], banc.Attentes.Select(a => (int)a.TotalSeconds));
        Assert.Single(banc.Stockage.LireFile());
    }

    [Fact]
    public async Task Une_API_coupee_garde_le_Rapport_en_file_annonce_l_echec_une_fois_puis_envoie_au_retour_du_reseau()
    {
        var banc = new Banc();
        banc.Api.Coupee = true;
        // Le réseau revient pendant l'attente de backoff n° 5.
        banc.AuxAttentes = n =>
        {
            if (n != 5) return;
            banc.Api.Coupee = false;
            banc.Api.Repond("POST", "/companion/CODE/rapports", HttpStatusCode.OK, Traite("Accepte"));
        };

        await banc.Envoi().SoumettreAsync(UnRapport(), banc.Cts.Token);

        Assert.Equal([IssueEnvoi.Erreur, IssueEnvoi.Accepte], banc.Resultats.Select(r => r.Issue));
        Assert.True(banc.Resultats[0].ProcedureManuelle);
        Assert.Empty(banc.Stockage.LireFile());
        Assert.Equal(6, banc.Api.Requetes.Count(r => r.Methode == "POST"));
    }

    [Fact]
    public async Task Un_Rapport_reste_en_file_apres_un_arret_est_renvoye_au_relancement_sans_second_Fichier()
    {
        var dossier = Directory.CreateTempSubdirectory().FullName;
        try
        {
            StockageEnvoiDisque Disque() => new(Path.Combine(dossier, "donnees"), Path.Combine(dossier, "Rapports"));

            var avant = new Banc { AnnuleApresAttentes = 1 };
            avant.Api.Coupee = true;
            await avant.Envoi(Disque()).SoumettreAsync(UnRapport("EUW1_55"), avant.Cts.Token);
            Assert.Equal("EUW1_55", Assert.Single(Disque().LireFile()).RiotMatchId);

            var apres = new Banc();
            apres.Api.Repond("POST", "/companion/CODE/rapports", HttpStatusCode.OK, Traite("EnRevue"));
            await apres.Envoi(Disque()).ReprendreAsync(apres.Cts.Token);

            Assert.Equal(IssueEnvoi.EnRevue, Assert.Single(apres.Resultats).Issue);
            Assert.Empty(Disque().LireFile());
            var requete = Assert.Single(apres.Api.Requetes, r => r.Methode == "POST");
            Assert.Equal(File.ReadAllText(Path.Combine(dossier, "Rapports", "rapport-EUW1_55.json")), requete.Corps);
        }
        finally
        {
            Directory.Delete(dossier, recursive: true);
        }
    }

    private const string PseudosJson = """{"superadministrateurs":["Thomas"],"administrateurs":["Sam","Lou"]}""";

    [Fact]
    public async Task Les_pseudos_sont_lus_au_lancement_et_gardes_en_cache()
    {
        var banc = new Banc();
        banc.Api.Repond("GET", "/companion/administrateurs", HttpStatusCode.OK, PseudosJson);

        await banc.Envoi().DemarrerAsync(banc.Cts.Token);

        var pseudos = banc.Stockage.LirePseudos();
        Assert.Equal(["Thomas"], pseudos!.Superadministrateurs);
        Assert.Equal(["Sam", "Lou"], pseudos.Administrateurs);
    }

    [Fact]
    public async Task Les_pseudos_sont_rafraichis_apres_un_envoi_reussi()
    {
        var banc = new Banc();
        banc.Api.Repond("POST", "/companion/CODE/rapports", HttpStatusCode.OK, Traite("Accepte"));
        banc.Api.Repond("GET", "/companion/administrateurs", HttpStatusCode.OK, PseudosJson);

        await banc.Envoi().SoumettreAsync(UnRapport(), banc.Cts.Token);

        Assert.Equal(["Sam", "Lou"], banc.Stockage.LirePseudos()!.Administrateurs);
    }

    [Fact]
    public async Task API_injoignable_le_dernier_pseudo_connu_reste_affiche()
    {
        var banc = new Banc();
        banc.Stockage.EcrirePseudos(new PseudosAdministrateurs(["Ancien"], []));
        banc.Api.Coupee = true;
        var envoi = banc.Envoi();

        await envoi.ActualiserPseudosAsync(banc.Cts.Token);

        Assert.Equal(["Ancien"], envoi.Pseudos!.Superadministrateurs);
    }
}
