using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace Shameless.Companion.Core.Tests;

// Seam : SurveillanceFinDeGame, avec un IClientLol simulé (jamais le vrai client) et les captures réelles.
public class SurveillanceTests
{
    private const string EogUri = "/lol-end-of-game/v1/eog-stats-block";
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly ClientLolSimule _client = new();
    private readonly JournalEnMemoire _journal = new();
    private readonly List<RapportDeGame> _rapports = [];
    private readonly List<bool> _etats = [];

    private SurveillanceFinDeGame Surveillance()
    {
        var s = new SurveillanceFinDeGame(_client, _journal, new OptionsSurveillance(3, TimeSpan.FromMilliseconds(5)));
        s.RapportConstruit += r => { lock (_rapports) _rapports.Add(r); };
        s.ClientDetecteChange += d => { lock (_etats) _etats.Add(d); };
        return s;
    }

    private async Task Executer(params EvenementClientLol[] evenements)
    {
        foreach (var e in evenements) _client.Evenements.Writer.TryWrite(e);
        _client.Evenements.Writer.Complete();
        await Surveillance().ExecuterAsync(Ct);
    }

    private static EvenementClientLol.Message FinDeGame(string type = "Update") =>
        new(EogUri, type, JsonDocument.Parse("""{"gameId":8019035742}""").RootElement.Clone());

    [Fact]
    public async Task La_fin_de_game_construit_un_Rapport_depuis_le_bloc_et_l_historique()
    {
        _client.Reponses[EogUri] = [new(200, Fixtures.Eog())];
        _client.Reponses["/lol-match-history/v1/games/8019035742"] = [new(200, Fixtures.Historique())];

        await Executer(FinDeGame());

        var rapport = Assert.Single(_rapports);
        Assert.Equal("EUW1_8019035742", rapport.RiotMatchId);
        Assert.Equal(10, rapport.Joueurs.Count);
    }

    [Fact]
    public async Task L_evenement_de_fin_de_game_recu_deux_fois_ne_donne_qu_un_Rapport()
    {
        _client.Reponses[EogUri] = [new(200, Fixtures.Eog())];
        _client.Reponses["/lol-match-history/v1/games/8019035742"] = [new(200, Fixtures.Historique())];

        await Executer(FinDeGame("Create"), FinDeGame("Update"));

        Assert.Single(_rapports);
        Assert.Contains(_journal.Lignes, l => l.Contains("déjà traitée") && l.Contains("8019035742"));
    }

    [Fact]
    public async Task L_historique_pas_encore_pret_est_relance()
    {
        _client.Reponses[EogUri] = [new(200, Fixtures.Eog())];
        _client.Reponses["/lol-match-history/v1/games/8019035742"] =
            [new(404, "{}"), new(404, "{}"), new(200, Fixtures.Historique())];

        await Executer(FinDeGame());

        Assert.Single(_rapports);
    }

    [Fact]
    public async Task Sans_historique_apres_les_essais_pas_de_Rapport_et_le_journal_le_dit()
    {
        _client.Reponses[EogUri] = [new(200, Fixtures.Eog())];
        _client.Reponses["/lol-match-history/v1/games/8019035742"] = [new(404, "{}")];

        await Executer(FinDeGame());

        Assert.Empty(_rapports);
        Assert.Contains(_journal.Lignes, l => l.Contains("historique") && l.Contains("8019035742"));
    }

    [Fact]
    public async Task Une_partie_qui_n_est_pas_une_custom_est_ignoree_et_journalisee_sans_lire_l_historique()
    {
        var eog = JsonNode.Parse(Fixtures.Eog())!;
        eog["gameType"] = "MATCHED_GAME";
        _client.Reponses[EogUri] = [new(200, eog.ToJsonString())];

        await Executer(FinDeGame());

        Assert.Empty(_rapports);
        Assert.Contains(_journal.Lignes, l => l.Contains("partie ignorée") && l.Contains("MATCHED_GAME"));
        Assert.DoesNotContain(_client.Lectures, l => l.Contains("match-history"));
    }

    [Fact]
    public async Task Les_joueurs_sans_PUUID_sont_signales_au_journal_et_le_Rapport_part_quand_meme()
    {
        var eog = JsonNode.Parse(Fixtures.Eog())!;
        eog["teams"]![0]!["players"]![0]!["puuid"] = "";
        _client.Reponses[EogUri] = [new(200, eog.ToJsonString())];
        _client.Reponses["/lol-match-history/v1/games/8019035742"] = [new(200, Fixtures.Historique())];

        await Executer(FinDeGame());

        Assert.Single(_rapports);
        Assert.Contains(_journal.Lignes, l => l.Contains("sans PUUID") && l.Contains("1"));
    }

    [Fact]
    public async Task Le_changement_d_etat_du_client_est_signale()
    {
        await Executer(new EvenementClientLol.Detecte(), new EvenementClientLol.Perdu(), new EvenementClientLol.Detecte());

        Assert.Equal([true, false, true], _etats);
    }

    [Fact]
    public async Task Les_autres_evenements_ne_declenchent_rien()
    {
        await Executer(new EvenementClientLol.Message("/lol-gameflow/v1/gameflow-phase", "Update", JsonDocument.Parse("\"InProgress\"").RootElement.Clone()));

        Assert.Empty(_client.Lectures);
    }

    [Fact]
    public async Task La_phase_EndOfGame_declenche_la_lecture_comme_le_bloc_de_fin_de_game()
    {
        _client.Reponses[EogUri] = [new(200, Fixtures.Eog())];
        _client.Reponses["/lol-match-history/v1/games/8019035742"] = [new(200, Fixtures.Historique())];

        await Executer(new EvenementClientLol.Message("/lol-gameflow/v1/gameflow-phase", "Update", JsonDocument.Parse("\"EndOfGame\"").RootElement.Clone()));

        Assert.Single(_rapports);
    }
}

internal sealed class JournalEnMemoire : IJournal
{
    public List<string> Lignes { get; } = [];
    public void Ecrire(string ligne) { lock (Lignes) Lignes.Add(ligne); }
}

internal sealed class ClientLolSimule : IClientLol
{
    public Channel<EvenementClientLol> Evenements { get; } = Channel.CreateUnbounded<EvenementClientLol>();

    /// <summary>Réponses par chemin : jouées dans l'ordre, la dernière se répète.</summary>
    public Dictionary<string, List<ReponseLcu>> Reponses { get; } = [];

    public List<string> Lectures { get; } = [];

    public IAsyncEnumerable<EvenementClientLol> SuivreAsync(CancellationToken ct) => Evenements.Reader.ReadAllAsync(ct);

    public Task<ReponseLcu> LireAsync(string chemin, CancellationToken ct)
    {
        lock (Lectures)
        {
            Lectures.Add(chemin);
            if (!Reponses.TryGetValue(chemin, out var liste)) return Task.FromResult(new ReponseLcu(404, "{}"));
            var reponse = liste[0];
            if (liste.Count > 1) liste.RemoveAt(0);
            return Task.FromResult(reponse);
        }
    }
}
