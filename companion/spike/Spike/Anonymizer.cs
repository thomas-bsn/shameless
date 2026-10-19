using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spike;

/// <summary>
/// Remplace pseudos, tags, PUUID et ids de compte par des valeurs fictives, de façon cohérente
/// (même vraie valeur, même fausse valeur, d'un fichier à l'autre). Les stats, champions et
/// <c>gameId</c> restent intacts.
/// </summary>
public static class Anonymizer
{
    private enum Kind { Name, Tag, Puuid, Id }

    private static readonly Dictionary<string, Kind> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["summonerName"] = Kind.Name,
        ["gameName"] = Kind.Name,
        ["riotIdGameName"] = Kind.Name,
        ["playerName"] = Kind.Name,
        ["summonerInternalName"] = Kind.Name,
        ["internalName"] = Kind.Name,
        ["displayName"] = Kind.Name,
        ["tagLine"] = Kind.Tag,
        ["riotIdTagLine"] = Kind.Tag,
        ["riotIdTagline"] = Kind.Tag,
        ["puuid"] = Kind.Puuid,
        ["summonerId"] = Kind.Id,
        ["accountId"] = Kind.Id,
        ["currentAccountId"] = Kind.Id,
        ["originalAccountNumber"] = Kind.Id,
    };

    public static IReadOnlyList<string> Anonymize(IReadOnlyList<string> jsonDocuments)
    {
        var nodes = jsonDocuments.Select(j => JsonNode.Parse(j)).ToList();
        var map = new Dictionary<(Kind, string), string>();
        var counters = new Dictionary<Kind, int>();

        foreach (var node in nodes) Collect(node, map, counters);

        // Pour retrouver un pseudo ou un PUUID glissé dans une autre chaîne : du plus long au plus court.
        var textual = map
            .Where(kv => kv.Key.Item1 is Kind.Name or Kind.Puuid && kv.Key.Item2.Length >= 3
                         || kv.Key.Item1 == Kind.Id && kv.Key.Item2.Length >= 8 && !long.TryParse(kv.Key.Item2, out _))
            .OrderByDescending(kv => kv.Key.Item2.Length)
            .Select(kv => (Real: kv.Key.Item2, Fake: kv.Value))
            .ToList();

        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        return nodes.Select(n => Replace(n, map, textual)?.ToJsonString(options) ?? "null").ToList();
    }

    public static void AnonymizeDirectory(string source, string destination)
    {
        // Seuls les JSON capturés sont repris : les événements bruts (.jsonl) restent chez toi.
        var files = Directory.GetFiles(source, "*.json", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal).ToList();
        var results = Anonymize(files.Select(File.ReadAllText).ToList());

        for (var i = 0; i < files.Count; i++)
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, files[i]));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, results[i]);
        }
    }

    private static void Collect(JsonNode? node, Dictionary<(Kind, string), string> map, Dictionary<Kind, int> counters)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (SensitiveKeys.TryGetValue(key, out var kind) && value is JsonValue v && TryRead(v, out var real) && real != "")
                        Register(kind, real, map, counters);
                    else
                        Collect(value, map, counters);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr) Collect(item, map, counters);
                break;
        }
    }

    private static void Register(Kind kind, string real, Dictionary<(Kind, string), string> map, Dictionary<Kind, int> counters)
    {
        if (map.ContainsKey((kind, real))) return;
        var n = counters[kind] = counters.GetValueOrDefault(kind) + 1;
        map[(kind, real)] = kind switch
        {
            Kind.Name => $"Joueur{n}",
            Kind.Tag => $"TAG{n}",
            Kind.Puuid => $"anon-puuid-{n:D4}",
            _ => (900_000 + n).ToString(),
        };
    }

    private static JsonNode? Replace(JsonNode? node, Dictionary<(Kind, string), string> map, List<(string Real, string Fake)> textual)
    {
        switch (node)
        {
            case JsonObject obj:
                var copy = new JsonObject();
                foreach (var (key, value) in obj)
                {
                    if (SensitiveKeys.TryGetValue(key, out var kind) && value is JsonValue v && TryRead(v, out var real)
                        && map.TryGetValue((kind, real), out var fake))
                    {
                        // Un id numérique reste un nombre.
                        copy[key] = kind == Kind.Id && v.GetValueKind() == JsonValueKind.Number
                            ? JsonValue.Create(long.Parse(fake))
                            : JsonValue.Create(fake);
                    }
                    else
                        copy[key] = Replace(value, map, textual);
                }
                return copy;
            case JsonArray arr:
                var list = new JsonArray();
                foreach (var item in arr) list.Add(Replace(item, map, textual));
                return list;
            case JsonValue val when val.GetValueKind() == JsonValueKind.String:
                var s = val.GetValue<string>();
                foreach (var (r, f) in textual) s = s.Replace(r, f, StringComparison.Ordinal);
                return JsonValue.Create(s);
            default:
                return node?.DeepClone();
        }
    }

    private static bool TryRead(JsonValue value, out string text)
    {
        switch (value.GetValueKind())
        {
            case JsonValueKind.String:
                text = value.GetValue<string>();
                return true;
            case JsonValueKind.Number:
                text = value.ToJsonString();
                return true;
            default:
                text = "";
                return false;
        }
    }
}
