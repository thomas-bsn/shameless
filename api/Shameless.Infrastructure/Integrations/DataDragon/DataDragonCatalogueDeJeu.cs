using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Integrations.DataDragon;

/// <summary>
/// Le catalogue des objets, sorts d'invocateur et runes lu sur Data Dragon (données statiques publiques,
/// en français) : la dernière version de versions.json, puis ses item.json, summoner.json et
/// runesReforged.json. Gardé en mémoire quelques heures. Toute panne de Data Dragon devient une
/// <see cref="CatalogueIndisponibleException"/>.
/// </summary>
public sealed class DataDragonCatalogueDeJeu : ICatalogueDeJeu
{
    private const string Langue = "fr_FR";
    private static readonly TimeSpan DureeDuCache = TimeSpan.FromHours(6);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private CatalogueDeJeu? _catalogue;
    private DateTimeOffset _expiration;

    public DataDragonCatalogueDeJeu(IHttpClientFactory httpClientFactory, TimeProvider clock)
    {
        _httpClientFactory = httpClientFactory;
        _clock = clock;
    }

    public async Task<CatalogueDeJeu> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_catalogue is { } enCache && _clock.GetUtcNow() < _expiration) return enCache;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_catalogue is { } dejaCharge && _clock.GetUtcNow() < _expiration) return dejaCharge;

            _catalogue = await ChargerOuIndisponibleAsync(cancellationToken);
            _expiration = _clock.GetUtcNow() + DureeDuCache;
            return _catalogue;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<CatalogueDeJeu> ChargerOuIndisponibleAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await ChargerAsync(cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException
                                      || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new CatalogueIndisponibleException("Data Dragon est injoignable ou sa réponse est illisible.", e);
        }
    }

    private async Task<CatalogueDeJeu> ChargerAsync(CancellationToken cancellationToken)
    {
        var http = _httpClientFactory.CreateClient(DataDragonChampionCatalog.HttpClientName);
        var racine = http.BaseAddress ?? DataDragonChampionCatalog.BaseAddress;

        var versions = await http.GetFromJsonAsync<List<string>>("api/versions.json", cancellationToken);
        var version = versions?.FirstOrDefault()
            ?? throw new CatalogueIndisponibleException("Data Dragon n'a renvoyé aucune version.");
        var dossier = $"cdn/{version}/data/{Langue}";

        var items = await http.GetFromJsonAsync<FichierDeDonnees>($"{dossier}/item.json", cancellationToken);
        var sorts = await http.GetFromJsonAsync<FichierDeDonnees>($"{dossier}/summoner.json", cancellationToken);
        var arbres = await http.GetFromJsonAsync<List<ArbreEntree>>($"{dossier}/runesReforged.json", cancellationToken);
        if (items?.Data is not { Count: > 0 } || sorts?.Data is not { Count: > 0 } || arbres is not { Count: > 0 })
            throw new CatalogueIndisponibleException($"Data Dragon n'a renvoyé aucun objet, sort ou rune pour la version {version}.");

        string Icone(string chemin) => new Uri(racine, chemin).ToString();

        return new CatalogueDeJeu(
            version,
            items.Data
                .Where(e => int.TryParse(e.Key, CultureInfo.InvariantCulture, out _) && e.Value.Image is not null)
                .Select(e => new ObjetDuCatalogue(int.Parse(e.Key, CultureInfo.InvariantCulture), e.Value.Name,
                    Icone($"cdn/{version}/img/item/{e.Value.Image!.Full}")))
                .OrderBy(o => o.Id)
                .ToList(),
            sorts.Data.Values
                .Where(s => int.TryParse(s.Key, CultureInfo.InvariantCulture, out _) && s.Image is not null)
                .Select(s => new SortDuCatalogue(int.Parse(s.Key!, CultureInfo.InvariantCulture), s.Name,
                    Icone($"cdn/{version}/img/spell/{s.Image!.Full}")))
                .OrderBy(s => s.Id)
                .ToList(),
            arbres
                .Select(a => new ArbreDuCatalogue(a.Id, a.Name, Icone($"cdn/img/{a.Icon}"),
                    (a.Slots ?? []).SelectMany(slot => slot.Runes ?? [])
                        .Select(r => new RuneDuCatalogue(r.Id, r.Name, Icone($"cdn/img/{r.Icon}")))
                        .ToList()))
                .ToList());
    }

    private sealed record FichierDeDonnees([property: JsonPropertyName("data")] Dictionary<string, Entree>? Data);

    private sealed record Entree(
        [property: JsonPropertyName("key")] string? Key,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("image")] ImageEntree? Image);

    private sealed record ImageEntree([property: JsonPropertyName("full")] string Full);

    private sealed record ArbreEntree(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("icon")] string Icon,
        [property: JsonPropertyName("slots")] List<SlotEntree>? Slots);

    private sealed record SlotEntree([property: JsonPropertyName("runes")] List<RuneEntree>? Runes);

    private sealed record RuneEntree(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("icon")] string Icon);
}
