using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Integrations.DataDragon;

/// <summary>
/// Le catalogue des champions lu sur Data Dragon (données statiques publiques, sans clé Riot) :
/// la dernière version de versions.json, puis son champion.json. Gardé en mémoire quelques heures.
/// </summary>
public sealed class DataDragonChampionCatalog : IChampionCatalog
{
    public const string HttpClientName = "DataDragon";
    public static readonly Uri BaseAddress = new("https://ddragon.leagueoflegends.com/");
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

            _champions = await LoadAsync(cancellationToken);
            _expiration = _clock.GetUtcNow() + DureeDuCache;
            return _champions;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyList<Champion>> LoadAsync(CancellationToken cancellationToken)
    {
        var http = _httpClientFactory.CreateClient(HttpClientName);

        var versions = await http.GetFromJsonAsync<List<string>>("api/versions.json", cancellationToken);
        var version = versions?.FirstOrDefault()
            ?? throw new InvalidOperationException("Data Dragon n'a renvoyé aucune version.");

        var catalogue = await http.GetFromJsonAsync<ChampionFile>(
            $"cdn/{version}/data/{Langue}/champion.json", cancellationToken);
        if (catalogue?.Data is not { Count: > 0 } data)
            throw new InvalidOperationException($"Data Dragon n'a renvoyé aucun champion pour la version {version}.");

        return data.Values
            .Select(c => new Champion(
                c.Id,
                c.Key,
                c.Name,
                new Uri(http.BaseAddress ?? BaseAddress, $"cdn/{version}/img/champion/{c.Image.Full}").ToString()))
            .OrderBy(c => c.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("fr-FR"), ignoreCase: true))
            .ToList();
    }

    private sealed record ChampionFile([property: JsonPropertyName("data")] Dictionary<string, ChampionEntry>? Data);

    private sealed record ChampionEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("image")] ChampionImage Image);

    private sealed record ChampionImage([property: JsonPropertyName("full")] string Full);
}
