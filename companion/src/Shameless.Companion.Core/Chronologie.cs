using System.Text.Json;

namespace Shameless.Companion.Core;

/// <summary>L'état d'un joueur à un instant (JoueurDuPointRequest) : les valeurs lues dans l'API locale du jeu, rattachées à un PUUID.</summary>
public sealed record JoueurDuPoint(string Puuid, int Kills, int Deaths, int Assists, int Cs, int Level, int Vision, int ItemsValue, bool Dead);

/// <summary><paramref name="T"/> : secondes depuis le début de la partie.</summary>
public sealed record PointDeChronologie(int T, IReadOnlyList<JoueurDuPoint> Joueurs);

/// <summary>Un objectif pris (EvenementDeChronologieRequest) : <paramref name="Type"/> dragon, herald, baron, turret ou inhibitor ; <paramref name="Side"/> blue ou red.</summary>
public sealed record EvenementDeChronologie(int T, string Type, string Side);

/// <summary>La chronologie facultative du Rapport (ChronologieRequest, ADR 0022). Rien n'y est calculé : des lectures datées.</summary>
public sealed record ChronologieDuRapport(IReadOnlyList<PointDeChronologie> Points, IReadOnlyList<EvenementDeChronologie> Evenements);

/// <summary>Ce que l'API locale du jeu dit d'un joueur, valeurs brutes (les scores de vision à virgule sont tronqués).</summary>
public sealed record ValeursDuJoueur(int Kills, int Deaths, int Assists, int Cs, int Level, int Vision, int ItemsValue, bool Dead);

/// <param name="Cles">Les noms par lesquels les événements et le bloc de fin de game désignent ce joueur (Riot ID, pseudo).</param>
/// <param name="Valeurs">Null quand une valeur manque ou est illisible : le joueur est alors absent du point, rien n'est deviné.</param>
public sealed record JoueurEnPartie(IReadOnlyList<string> Cles, string? Side, ValeursDuJoueur? Valeurs)
{
    public string? RiotId => Cles.FirstOrDefault();
}

public sealed record InstantaneDePartie(int Temps, IReadOnlyList<JoueurEnPartie> Joueurs, IReadOnlyList<EvenementDeChronologie> Evenements);

/// <summary>Le seam de l'API locale du jeu (Live Client Data API, en partie seulement). Le module HTTP vit dans Shameless.Companion.Lcu.</summary>
public interface IPartieEnCours
{
    /// <summary>Le JSON de <c>/liveclientdata/allgamedata</c>, ou null s'il n'y a pas de partie en cours (API fermée, statut d'erreur).</summary>
    Task<string?> LireAsync(CancellationToken ct);
}

/// <summary>Lit la réponse de <c>/liveclientdata/allgamedata</c> (fonction pure). Null si le format n'est pas celui attendu.</summary>
public static class LiveClient
{
    private static readonly Dictionary<string, string> TypesDEvenements = new()
    {
        ["DragonKill"] = "dragon",
        ["HeraldKill"] = "herald",
        ["BaronKill"] = "baron",
        ["TurretKilled"] = "turret",
        ["InhibKilled"] = "inhibitor",
    };

    private const int NiveauMax = 18;

    public static InstantaneDePartie? Lire(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return Lire(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static InstantaneDePartie? Lire(JsonElement racine)
    {
        if (racine.ValueKind != JsonValueKind.Object) return null;
        if (!racine.TryGetProperty("gameData", out var donnees) || donnees.ValueKind != JsonValueKind.Object
            || !donnees.TryGetProperty("gameTime", out var temps) || temps.ValueKind != JsonValueKind.Number
            || !temps.TryGetDouble(out var secondes) || secondes < 0)
            return null;
        if (!racine.TryGetProperty("allPlayers", out var joueurs) || joueurs.ValueKind != JsonValueKind.Array || joueurs.GetArrayLength() == 0)
            return null;

        var lus = new List<JoueurEnPartie>();
        foreach (var joueur in joueurs.EnumerateArray())
        {
            if (joueur.ValueKind != JsonValueKind.Object) return null;
            lus.Add(LireJoueur(joueur));
        }

        var evenements = new List<EvenementDeChronologie>();
        if (racine.TryGetProperty("events", out var bloc))
        {
            if (bloc.ValueKind != JsonValueKind.Object || !bloc.TryGetProperty("Events", out var liste) || liste.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var e in liste.EnumerateArray())
                if (LireEvenement(e, lus) is { } evenement) evenements.Add(evenement);
        }

        return new InstantaneDePartie((int)secondes, lus, evenements);
    }

    private static JoueurEnPartie LireJoueur(JsonElement joueur)
    {
        var nom = Texte(joueur, "riotIdGameName");
        var tag = Texte(joueur, "riotIdTagLine");
        var cles = new List<string>();
        Ajouter(cles, Texte(joueur, "riotId"));
        Ajouter(cles, nom is not null && tag is not null ? $"{nom}#{tag}" : null);
        Ajouter(cles, Texte(joueur, "summonerName"));
        Ajouter(cles, nom);

        var side = Texte(joueur, "team") switch { "ORDER" => "blue", "CHAOS" => "red", _ => null };
        return new JoueurEnPartie(cles, side, LireValeurs(joueur));
    }

    private static void Ajouter(List<string> cles, string? cle)
    {
        if (!string.IsNullOrWhiteSpace(cle) && !cles.Contains(cle, StringComparer.OrdinalIgnoreCase)) cles.Add(cle);
    }

    private static ValeursDuJoueur? LireValeurs(JsonElement joueur)
    {
        if (!joueur.TryGetProperty("scores", out var scores) || scores.ValueKind != JsonValueKind.Object) return null;
        if (Entier(scores, "kills") is not { } kills || Entier(scores, "deaths") is not { } deaths
            || Entier(scores, "assists") is not { } assists || Entier(scores, "creepScore") is not { } cs
            || !scores.TryGetProperty("wardScore", out var ward) || ward.ValueKind != JsonValueKind.Number
            || !ward.TryGetDouble(out var vision) || vision < 0 || vision > int.MaxValue)
            return null;
        if (Entier(joueur, "level") is not { } niveau || niveau is < 1 or > NiveauMax) return null;
        if (!joueur.TryGetProperty("isDead", out var mort) || mort.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return null;
        if (ValeurDesObjets(joueur) is not { } objets) return null;
        return new ValeursDuJoueur(kills, deaths, assists, cs, niveau, (int)vision, objets, mort.GetBoolean());
    }

    /// <summary>La somme prix x quantité des objets ; null dès qu'un objet n'a pas de prix ou de quantité lisibles.</summary>
    private static int? ValeurDesObjets(JsonElement joueur)
    {
        if (!joueur.TryGetProperty("items", out var objets) || objets.ValueKind != JsonValueKind.Array) return null;
        long total = 0;
        foreach (var objet in objets.EnumerateArray())
        {
            if (objet.ValueKind != JsonValueKind.Object || Entier(objet, "price") is not { } prix || Entier(objet, "count") is not { } quantite)
                return null;
            total += (long)prix * quantite;
        }
        return total <= int.MaxValue ? (int)total : null;
    }

    private static EvenementDeChronologie? LireEvenement(JsonElement e, List<JoueurEnPartie> joueurs)
    {
        if (e.ValueKind != JsonValueKind.Object || Texte(e, "EventName") is not { } nom || !TypesDEvenements.TryGetValue(nom, out var type)) return null;
        if (!e.TryGetProperty("EventTime", out var temps) || temps.ValueKind != JsonValueKind.Number || !temps.TryGetDouble(out var t) || t < 0)
            return null;

        // Une tourelle ou un inhibiteur dit à qui il appartient (T1 = ORDER, T2 = CHAOS) : l'autre équipe l'a pris.
        var side = type switch
        {
            "turret" => SideDeLAdversaire(Texte(e, "TurretKilled")),
            "inhibitor" => SideDeLAdversaire(Texte(e, "InhibKilled")),
            _ => null,
        };
        side ??= Texte(e, "KillerName") is { } tueur ? SideDuTueur(tueur, joueurs) : null;
        return side is null ? null : new EvenementDeChronologie((int)t, type, side);
    }

    private static string? SideDeLAdversaire(string? nomDeStructure)
    {
        if (nomDeStructure is null) return null;
        bool Est(string equipe) => nomDeStructure.Contains($"_{equipe}_", StringComparison.Ordinal) || nomDeStructure.EndsWith($"_{equipe}", StringComparison.Ordinal);
        return Est("T1") ? "red" : Est("T2") ? "blue" : null;
    }

    private static string? SideDuTueur(string tueur, List<JoueurEnPartie> joueurs) =>
        joueurs.FirstOrDefault(j => j.Cles.Contains(tueur, StringComparer.OrdinalIgnoreCase))?.Side;

    private static int? Entier(JsonElement objet, string cle) =>
        objet.TryGetProperty(cle, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && n >= 0 ? n : null;

    private static string? Texte(JsonElement objet, string cle) =>
        objet.TryGetProperty(cle, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
