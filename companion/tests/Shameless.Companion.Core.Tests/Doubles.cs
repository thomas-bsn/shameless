using System.Net;
using System.Text;

namespace Shameless.Companion.Core.Tests;

internal sealed class StockageEnMemoire : IStockageEnvoi
{
    private IReadOnlyList<RapportEnFile> _file = [];
    private PseudosAdministrateurs? _pseudos;
    public Dictionary<string, string> Fichiers { get; } = [];

    public string EcrireFichierRapport(string riotMatchId, string json)
    {
        var nom = $"rapport-{riotMatchId}.json";
        Fichiers[nom] = json;
        return "/rapports/" + nom;
    }

    public IReadOnlyList<RapportEnFile> LireFile() => _file;
    public void EcrireFile(IReadOnlyList<RapportEnFile> file) => _file = file.ToList();
    public PseudosAdministrateurs? LirePseudos() => _pseudos;
    public void EcrirePseudos(PseudosAdministrateurs pseudos) => _pseudos = pseudos;
}

internal sealed class IdentifiantsFixes(IdentifiantsCompanion? valeur) : IStockageIdentifiants
{
    public IdentifiantsCompanion? Lire() => valeur;
    public void Ecrire(IdentifiantsCompanion identifiants) { }
    public void Effacer() { }
}

internal sealed record RequeteRecue(string Methode, string Url, string Corps);

/// <summary>Une API simulée au niveau HTTP : une réponse par (méthode, chemin), ou la connexion coupée.</summary>
internal sealed class ApiSimulee : HttpMessageHandler
{
    private readonly Dictionary<(string, string), (HttpStatusCode, string)> _reponses = [];
    private readonly object _verrou = new();
    private readonly List<RequeteRecue> _requetes = [];

    public volatile bool Coupee;

    public IReadOnlyList<RequeteRecue> Requetes
    {
        get { lock (_verrou) return _requetes.ToList(); }
    }

    public ApiSimulee Repond(string methode, string chemin, HttpStatusCode statut, string corps)
    {
        lock (_verrou) _reponses[(methode, chemin)] = (statut, corps);
        return this;
    }

    public HttpClient Client() => new(this);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var corps = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        lock (_verrou) _requetes.Add(new RequeteRecue(request.Method.Method, request.RequestUri!.ToString(), corps));
        if (Coupee) throw new HttpRequestException("connexion refusée");
        (HttpStatusCode, string) reponse;
        lock (_verrou)
            if (!_reponses.TryGetValue((request.Method.Method, request.RequestUri!.AbsolutePath), out reponse))
                reponse = (HttpStatusCode.NotFound, "");
        return new HttpResponseMessage(reponse.Item1) { Content = new StringContent(reponse.Item2, Encoding.UTF8, "application/json") };
    }
}
