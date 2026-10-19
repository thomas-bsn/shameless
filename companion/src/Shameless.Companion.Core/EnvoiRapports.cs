using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Shameless.Companion.Core;

/// <summary>Un Rapport en attente d'envoi : son corps JSON exact (celui du Fichier de Rapport) et où est ce fichier.</summary>
public sealed record RapportEnFile(string RiotMatchId, string Json, string CheminFichier);

/// <summary>Les pseudos à qui remettre un Fichier de Rapport (GET /companion/administrateurs).</summary>
public sealed record PseudosAdministrateurs(IReadOnlyList<string> Superadministrateurs, IReadOnlyList<string> Administrateurs);

/// <summary>Où l'envoi garde ses données : disque dans l'app, mémoire dans les tests.</summary>
public interface IStockageEnvoi
{
    /// <summary>Écrit <c>rapport-&lt;riotMatchId&gt;.json</c> (écrasé s'il existe) et renvoie son chemin.</summary>
    string EcrireFichierRapport(string riotMatchId, string json);

    IReadOnlyList<RapportEnFile> LireFile();
    void EcrireFile(IReadOnlyList<RapportEnFile> file);

    PseudosAdministrateurs? LirePseudos();
    void EcrirePseudos(PseudosAdministrateurs pseudos);
}

public enum IssueEnvoi
{
    Accepte,
    EnRevue,
    Rejete,

    /// <summary>400/404/409 : refus définitif, le Rapport est sorti de la file, le Fichier de Rapport est gardé.</summary>
    Refuse,

    /// <summary>API injoignable ou en erreur après plusieurs essais : le Rapport reste en file et sera renvoyé.</summary>
    Erreur,
}

/// <summary>Le résultat à montrer au joueur. <see cref="ProcedureManuelle"/> : afficher « donne le fichier à ... ».</summary>
public sealed record ResultatEnvoi(string RiotMatchId, IssueEnvoi Issue, string Message, string CheminFichier)
{
    public bool ProcedureManuelle => Issue is IssueEnvoi.Refuse or IssueEnvoi.Erreur;
}

/// <param name="DelaiInitial">Attente après le premier essai raté ; doublée à chaque essai raté.</param>
/// <param name="DelaiMax">Plafond du backoff.</param>
/// <param name="EssaisAvantEchec">Essais ratés (réseau, 5xx) avant d'annoncer l'échec et d'afficher la procédure manuelle.</param>
public sealed record OptionsEnvoi(TimeSpan DelaiInitial, TimeSpan DelaiMax, int EssaisAvantEchec)
{
    public static OptionsEnvoi ParDefaut { get; } = new(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5), 3);

    public TimeSpan DelaiApres(int essaisRates)
    {
        var facteur = Math.Pow(2, Math.Max(0, essaisRates - 1));
        return TimeSpan.FromSeconds(Math.Min(DelaiInitial.TotalSeconds * facteur, DelaiMax.TotalSeconds));
    }
}

/// <summary>
/// Envoie les Rapports à l'API : écrit le Fichier de Rapport, met en file (persistante), envoie, renvoie avec backoff
/// tant que l'API ne répond pas (l'API dédoublonne par riotMatchId : un renvoi est sûr) et lit les pseudos des
/// administrateurs pour la procédure d'échec (ADR 0020).
/// </summary>
public sealed class EnvoiRapports(
    IStockageEnvoi stockage,
    IStockageIdentifiants identifiants,
    HttpClient http,
    IJournal journal,
    OptionsEnvoi? options = null,
    Func<TimeSpan, CancellationToken, Task>? attendre = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly OptionsEnvoi _options = options ?? OptionsEnvoi.ParDefaut;
    private readonly Func<TimeSpan, CancellationToken, Task> _attendre = attendre ?? Task.Delay;
    private readonly object _verrou = new();
    private readonly Dictionary<string, int> _essaisRates = [];
    private readonly HashSet<string> _echecAnnonce = [];
    private Task _envoi = Task.CompletedTask;
    private bool _envoiEnCours;

    public event Action<ResultatEnvoi>? Resultat;
    public event Action<PseudosAdministrateurs>? PseudosChange;

    /// <summary>Le dernier couple de pseudos connu (cache disque si l'API n'a pas répondu depuis le lancement).</summary>
    public PseudosAdministrateurs? Pseudos => stockage.LirePseudos();

    /// <summary>Au lancement : rafraîchit les pseudos puis renvoie les Rapports restés en file.</summary>
    public async Task DemarrerAsync(CancellationToken ct = default)
    {
        await ActualiserPseudosAsync(ct);
        await ReprendreAsync(ct);
    }

    /// <summary>Renvoie la file maintenant (« Réessayer l'envoi », relancement). Finit quand la file est vide.</summary>
    public Task ReprendreAsync(CancellationToken ct = default)
    {
        lock (_verrou) return LancerEnvoi(ct);
    }

    /// <summary>Écrit le Fichier de Rapport, met le Rapport en file, puis l'envoie. La tâche rendue finit quand la file est vide.</summary>
    public Task SoumettreAsync(RapportDeGame rapport, CancellationToken ct = default)
    {
        var json = RapportJson.Serialiser(rapport);
        var chemin = stockage.EcrireFichierRapport(rapport.RiotMatchId, json);
        lock (_verrou)
        {
            var file = stockage.LireFile().Where(r => r.RiotMatchId != rapport.RiotMatchId).ToList();
            file.Add(new RapportEnFile(rapport.RiotMatchId, json, chemin));
            stockage.EcrireFile(file);
            return LancerEnvoi(ct);
        }
    }

    // Appelé sous le verrou.
    private Task LancerEnvoi(CancellationToken ct)
    {
        if (_envoiEnCours) return _envoi;
        _envoiEnCours = true;
        _envoi = Task.Run(() => ViderLaFileAsync(ct), CancellationToken.None);
        return _envoi;
    }

    private async Task ViderLaFileAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                IReadOnlyList<RapportEnFile> file;
                lock (_verrou)
                {
                    file = stockage.LireFile();
                    if (file.Count == 0)
                    {
                        _envoiEnCours = false;
                        return;
                    }
                }

                foreach (var rapport in file)
                    await EnvoyerAsync(rapport, ct);

                int rates;
                lock (_verrou)
                {
                    var restants = stockage.LireFile();
                    if (restants.Count == 0)
                    {
                        _envoiEnCours = false;
                        return;
                    }
                    rates = restants.Min(r => _essaisRates.GetValueOrDefault(r.RiotMatchId));
                }
                await _attendre(_options.DelaiApres(rates), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_verrou) _envoiEnCours = false;
        }
    }

    private async Task EnvoyerAsync(RapportEnFile rapport, CancellationToken ct)
    {
        var ids = identifiants.Lire();
        if (ids is null)
        {
            Rate(rapport, "Pas de Code Companion : connecte-toi pour envoyer le Rapport.");
            return;
        }

        var url = $"{ids.UrlInstance.TrimEnd('/')}/companion/{Uri.EscapeDataString(ids.Code)}/rapports";
        try
        {
            using var contenu = new StringContent(rapport.Json, Encoding.UTF8, "application/json");
            using var reponse = await http.PostAsync(url, contenu, ct);
            var statut = (int)reponse.StatusCode;
            if (reponse.IsSuccessStatusCode)
            {
                var traite = await reponse.Content.ReadFromJsonAsync<RapportTraite>(JsonOptions, ct);
                if (traite is null)
                {
                    Rate(rapport, "Réponse de l'API illisible.");
                    return;
                }
                Retirer(rapport);
                var (issue, message) = traite.Statut switch
                {
                    "Accepte" => (IssueEnvoi.Accepte, $"Rapport {rapport.RiotMatchId} accepté."),
                    "EnRevue" => (IssueEnvoi.EnRevue, $"Rapport {rapport.RiotMatchId} reçu, en revue : un administrateur le validera."),
                    _ => (IssueEnvoi.Rejete, $"Rapport {rapport.RiotMatchId} rejeté par l'API."),
                };
                journal.Ecrire(message);
                Resultat?.Invoke(new ResultatEnvoi(rapport.RiotMatchId, issue, message, rapport.CheminFichier));
                await ActualiserPseudosAsync(ct);
            }
            else if (statut is 400 or 404 or 409)
            {
                Retirer(rapport);
                var message = $"Rapport {rapport.RiotMatchId} refusé par l'API ({statut}) : {await LireTitreAsync(reponse, ct)}";
                journal.Ecrire(message);
                Resultat?.Invoke(new ResultatEnvoi(rapport.RiotMatchId, IssueEnvoi.Refuse, message, rapport.CheminFichier));
            }
            else
            {
                Rate(rapport, $"L'API a répondu {statut}.");
            }
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or UriFormatException
            || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            Rate(rapport, e.Message);
        }
    }

    private void Retirer(RapportEnFile rapport)
    {
        lock (_verrou)
        {
            stockage.EcrireFile(stockage.LireFile().Where(r => r.RiotMatchId != rapport.RiotMatchId).ToList());
            _essaisRates.Remove(rapport.RiotMatchId);
            _echecAnnonce.Remove(rapport.RiotMatchId);
        }
    }

    private void Rate(RapportEnFile rapport, string raison)
    {
        int rates;
        bool annoncer;
        lock (_verrou)
        {
            rates = _essaisRates[rapport.RiotMatchId] = _essaisRates.GetValueOrDefault(rapport.RiotMatchId) + 1;
            annoncer = rates >= _options.EssaisAvantEchec && _echecAnnonce.Add(rapport.RiotMatchId);
        }
        journal.Ecrire($"Envoi du Rapport {rapport.RiotMatchId} raté ({rates}) : {raison}");
        if (annoncer)
            Resultat?.Invoke(new ResultatEnvoi(rapport.RiotMatchId, IssueEnvoi.Erreur,
                $"Rapport {rapport.RiotMatchId} non envoyé : {raison} Nouvel essai automatique plus tard.", rapport.CheminFichier));
    }

    private static async Task<string> LireTitreAsync(HttpResponseMessage reponse, CancellationToken ct)
    {
        try
        {
            var probleme = await reponse.Content.ReadFromJsonAsync<Probleme>(JsonOptions, ct);
            return probleme?.Detail ?? probleme?.Title ?? reponse.ReasonPhrase ?? "";
        }
        catch (JsonException)
        {
            return reponse.ReasonPhrase ?? "";
        }
    }

    /// <summary>Relit les pseudos ; si l'API ne répond pas, le dernier connu reste en cache.</summary>
    public async Task ActualiserPseudosAsync(CancellationToken ct = default)
    {
        var ids = identifiants.Lire();
        if (ids is null) return;
        try
        {
            using var reponse = await http.GetAsync($"{ids.UrlInstance.TrimEnd('/')}/companion/administrateurs", ct);
            reponse.EnsureSuccessStatusCode();
            var lu = await reponse.Content.ReadFromJsonAsync<PseudosReponse>(JsonOptions, ct);
            if (lu is null) return;
            var pseudos = new PseudosAdministrateurs(lu.Superadministrateurs ?? [], lu.Administrateurs ?? []);
            stockage.EcrirePseudos(pseudos);
            PseudosChange?.Invoke(pseudos);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or UriFormatException
            || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            journal.Ecrire($"Pseudos des administrateurs non lus ({e.Message}) : le dernier connu est gardé.");
        }
    }

    private sealed record RapportTraite(string Statut);

    private sealed record Probleme(string? Title, string? Detail);

    private sealed record PseudosReponse(List<string>? Superadministrateurs, List<string>? Administrateurs);
}
