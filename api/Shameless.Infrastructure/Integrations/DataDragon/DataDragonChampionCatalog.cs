using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Integrations.DataDragon;

/// <summary>
/// Le catalogue des champions lu sur Data Dragon (données statiques publiques, sans clé Riot) :
/// la dernière version de versions.json, puis son champion.json. Gardé en mémoire quelques heures.
/// Toute panne de Data Dragon (erreur HTTP, délai dépassé, réponse illisible ou vide) devient une
/// <see cref="CatalogueIndisponibleException"/>.
/// </summary>
public sealed class DataDragonChampionCatalog : IChampionCatalog
{
    public const string HttpClientName = "DataDragon";
    public static readonly Uri BaseAddress = new("https://ddragon.leagueoflegends.com/");

    /// <summary>Meraki Analytics : les voies (positions) de chaque champion, que Data Dragon ne donne pas.</summary>
    public const string MerakiHttpClientName = "Meraki";
    public static readonly Uri MerakiBaseAddress = new("https://cdn.merakianalytics.com/");
    private const string MerakiChemin = "riot/lol/resources/latest/en-US/champions.json";
    // Sans les voies, on réessaie bientôt plutôt que d'attendre six heures.
    private static readonly TimeSpan DureeSansVoies = TimeSpan.FromMinutes(5);
    private const string Langue = "fr_FR";
    private static readonly TimeSpan DureeDuCache = TimeSpan.FromHours(6);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<Champion>? _champions;
    private DateTimeOffset _expiration;

    public DataDragonChampionCatalog(IHttpClientFactory httpClientFactory, TimeProvider clock)
    {
        _httpClientFactory = httpClientFactory;
        _clock = clock;
    }

    public async Task<IReadOnlyList<Champion>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_champions is { } enCache && _clock.GetUtcNow() < _expiration) return enCache;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_champions is { } dejaCharges && _clock.GetUtcNow() < _expiration) return dejaCharges;

            var champions = await LoadOrThrowIndisponibleAsync(cancellationToken);
            var voies = await LoadVoiesAsync(cancellationToken);
            _champions = champions
                .Select(c => c with { Voies = voies.GetValueOrDefault(c.Key, []) })
                .ToList();
            _expiration = _clock.GetUtcNow() + (voies.Count > 0 ? DureeDuCache : DureeSansVoies);
            return _champions;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyList<Champion>> LoadOrThrowIndisponibleAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await LoadAsync(cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException
                                      || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new CatalogueIndisponibleException("Data Dragon est injoignable ou sa réponse est illisible.", e);
        }
    }

    private async Task<IReadOnlyList<Champion>> LoadAsync(CancellationToken cancellationToken)
    {
        var http = _httpClientFactory.CreateClient(HttpClientName);

        var versions = await http.GetFromJsonAsync<List<string>>("api/versions.json", cancellationToken);
        var version = versions?.FirstOrDefault()
            ?? throw new CatalogueIndisponibleException("Data Dragon n'a renvoyé aucune version.");

        var catalogue = await http.GetFromJsonAsync<ChampionFile>(
            $"cdn/{version}/data/{Langue}/champion.json", cancellationToken);
        if (catalogue?.Data is not { Count: > 0 } data)
            throw new CatalogueIndisponibleException($"Data Dragon n'a renvoyé aucun champion pour la version {version}.");

        return data.Values
            .Select(c => new Champion(
                c.Id,
                c.Key,
                c.Name,
                new Uri(http.BaseAddress ?? BaseAddress, $"cdn/{version}/img/champion/{c.Image.Full}").ToString(),
                []))
            .OrderBy(c => c.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("fr-FR"), ignoreCase: true))
            .ToList();
    }

    /// <summary>
    /// Les voies de Meraki par clé Riot du champion. Toute panne de Meraki (réseau, délai, réponse illisible)
    /// donne un dictionnaire vide : le catalogue reste servi, les champions sont alors visibles sous toutes les voies.
    /// </summary>
    private async Task<Dictionary<string, IReadOnlyList<string>>> LoadVoiesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var http = _httpClientFactory.CreateClient(MerakiHttpClientName);
            var fichier = await http.GetFromJsonAsync<Dictionary<string, MerakiEntry>>(
                new Uri(http.BaseAddress ?? MerakiBaseAddress, MerakiChemin), cancellationToken);

            var voies = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var entree in fichier?.Values.AsEnumerable() ?? [])
            {
                var noms = (entree.Positions ?? [])
                    .Select(NomDeVoie).OfType<string>().Distinct().ToList();
                if (noms.Count > 0) voies[entree.Id.ToString()] = noms;
            }
            return voies;
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or NotSupportedException
                                      || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return [];
        }
    }

    private static string? NomDeVoie(string position) => position.ToUpperInvariant() switch
    {
        "TOP" => Voies.Top,
        "JUNGLE" => Voies.Jungle,
        "MIDDLE" => Voies.Mid,
        "BOTTOM" => Voies.Adc,
        "SUPPORT" => Voies.Support,
        _ => null
    };

    private sealed record MerakiEntry(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("positions")] List<string>? Positions);

    private sealed record ChampionFile([property: JsonPropertyName("data")] Dictionary<string, ChampionEntry>? Data);

    private sealed record ChampionEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("image")] ChampionImage Image);

    private sealed record ChampionImage([property: JsonPropertyName("full")] string Full);
}
