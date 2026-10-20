using System.Text.Json;

namespace Shameless.Application.Stats;

/// <summary>
/// Lecture défensive d'un objet JSON envoyé par le Companion (League Client API locale) : le contrat
/// du Companion n'est pas figé, alors chaque champ est cherché sous plusieurs noms (camelCase de
/// l'historique de match LCU / match-v5, UPPER_SNAKE du bloc de fin de game LCU), à la racine puis
/// dans un objet imbriqué (ex. <c>stats</c>, <c>timeline</c>). Un champ absent ou d'un autre type
/// donne null, sans jamais faire échouer la lecture.
/// </summary>
internal sealed class ChampsLcu
{
    private readonly JsonElement[] _objets;

    private ChampsLcu(JsonElement[] objets) => _objets = objets;

    /// <summary>Les champs de <paramref name="json"/>, null si ce n'est pas un objet JSON lisible.</summary>
    public static ChampsLcu? De(string? json, params string[] objetsImbriques)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        JsonElement racine;
        try
        {
            using var document = JsonDocument.Parse(json);
            racine = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
        if (racine.ValueKind != JsonValueKind.Object) return null;

        var objets = new List<JsonElement> { racine };
        foreach (var nom in objetsImbriques)
            if (racine.TryGetProperty(nom, out var imbrique) && imbrique.ValueKind == JsonValueKind.Object)
                objets.Add(imbrique);
        return new ChampsLcu([.. objets]);
    }

    private JsonElement? Premier(string[] noms, JsonValueKind type)
    {
        foreach (var objet in _objets)
        foreach (var nom in noms)
            if (objet.TryGetProperty(nom, out var valeur) && valeur.ValueKind == type)
                return valeur;
        return null;
    }

    /// <summary>Le premier nombre trouvé sous l'un des <paramref name="noms"/>, arrondi à l'entier.</summary>
    public int? Entier(params string[] noms)
    {
        if (Premier(noms, JsonValueKind.Number) is not { } valeur) return null;
        if (valeur.TryGetInt32(out var entier)) return entier;
        return valeur.TryGetDouble(out var reel) && reel is >= int.MinValue and <= int.MaxValue
            ? (int)Math.Round(reel)
            : null;
    }

    /// <summary>La première chaîne non vide trouvée sous l'un des <paramref name="noms"/>.</summary>
    public string? Texte(params string[] noms) =>
        noms.Select(nom => Premier([nom], JsonValueKind.String)?.GetString())
            .FirstOrDefault(texte => !string.IsNullOrWhiteSpace(texte));

    /// <summary>Le premier tableau trouvé sous l'un des <paramref name="noms"/> : ses nombres, null pour une case qui n'en est pas un (vide tolérée).</summary>
    public int?[]? Entiers(params string[] noms)
    {
        if (Premier(noms, JsonValueKind.Array) is not { } tableau) return null;
        return tableau.EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n) ? (int?)n : null)
            .ToArray();
    }
}
