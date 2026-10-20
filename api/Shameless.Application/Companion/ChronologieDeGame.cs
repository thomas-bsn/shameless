using System.Text.Json;
using Shameless.Application.Entities;

namespace Shameless.Application.Companion;

/// <summary>
/// La chronologie facultative d'un Rapport (ADR 0022) : des points datés (secondes depuis le début de la
/// partie) avec les valeurs de chaque joueur, et des événements d'objectifs datés. Le format est générique :
/// une source exacte pourra un jour la remplacer sans changer l'écran. Les champs numériques sont nullables
/// pour que l'absence d'un champ soit refusée, jamais lue comme zéro ; une chronologie gardée est valide.
/// </summary>
public sealed record ChronologieDeGame(IReadOnlyList<PointDeChronologie>? Points, IReadOnlyList<EvenementDeChronologie>? Evenements)
{
    public const int NombreMaxDePoints = 2000;
    public const int NombreMaxDEvenements = 500;
    public const int TempsMaxEnSecondes = 24 * 3600;
    public const int NiveauMax = 18;

    /// <summary>Les types d'événements acceptés.</summary>
    public static readonly IReadOnlyList<string> TypesDEvenements = ["dragon", "herald", "baron", "turret", "inhibitor"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Pourquoi cette chronologie est refusée, ou null ; <paramref name="puuids"/> sont ceux des dix joueurs du rapport.</summary>
    public string? Invalidite(IReadOnlySet<string> puuids)
    {
        if (Points is not { Count: > 0 })
            return "La chronologie porte au moins un point (sinon, ne l'envoyez pas).";
        if (Points.Count > NombreMaxDePoints)
            return $"La chronologie compte {NombreMaxDePoints} points au plus.";
        if (Points.Any(p => p == null))
            return "Chaque point de la chronologie est un objet.";

        var precedent = -1;
        foreach (var point in Points)
        {
            if (point.T is not { } t || t < 0 || t > TempsMaxEnSecondes)
                return $"Chaque point est daté en secondes depuis le début, entre 0 et {TempsMaxEnSecondes}.";
            if (t <= precedent)
                return "Les points de la chronologie sont classés par date croissante, sans date en double.";
            precedent = t;
            if (point.Joueurs is not { Count: > 0 })
                return "Chaque point de la chronologie décrit au moins un joueur.";
            if (point.Joueurs.Any(j => j == null))
                return "Chaque joueur d'un point est un objet.";
            if (point.Joueurs.Select(j => j.Puuid).Distinct().Count() != point.Joueurs.Count)
                return "Un joueur n'apparaît qu'une fois par point.";
            foreach (var joueur in point.Joueurs)
            {
                if (joueur.Puuid == null || !puuids.Contains(joueur.Puuid))
                    return "Chaque joueur de la chronologie est l'un des dix joueurs du rapport.";
                var invalide = joueur.Invalidite();
                if (invalide != null) return invalide;
            }
        }

        var evenements = Evenements ?? [];
        if (evenements.Count > NombreMaxDEvenements)
            return $"La chronologie compte {NombreMaxDEvenements} événements au plus.";
        foreach (var evenement in evenements)
        {
            if (evenement == null || evenement.T is not { } te || te < 0 || te > TempsMaxEnSecondes)
                return $"Chaque événement est daté en secondes depuis le début, entre 0 et {TempsMaxEnSecondes}.";
            if (evenement.Type == null || !TypesDEvenements.Contains(evenement.Type))
                return $"Le type d'un événement est l'un de : {string.Join(", ", TypesDEvenements)}.";
            if (evenement.Side is not (Sides.Blue or Sides.Red))
                return "Le side d'un événement est « blue » ou « red ».";
        }
        return null;
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static ChronologieDeGame? Lire(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<ChronologieDeGame>(json, Json);
}

/// <param name="T">Les secondes écoulées depuis le début de la partie.</param>
public sealed record PointDeChronologie(int? T, IReadOnlyList<JoueurDuPoint>? Joueurs);

/// <summary>
/// L'état d'un joueur à un instant : kills, morts, assists, CS, niveau, score de vision, valeur totale de ses
/// objets (en or) et s'il est mort à cet instant.
/// </summary>
public sealed record JoueurDuPoint(
    string? Puuid, int? Kills, int? Deaths, int? Assists, int? Cs, int? Level, int? Vision, int? ItemsValue, bool? Dead)
{
    public string? Invalidite()
    {
        if (Kills is null || Deaths is null || Assists is null || Cs is null || Level is null
            || Vision is null || ItemsValue is null || Dead is null)
            return "Chaque joueur d'un point donne kills, deaths, assists, cs, level, vision, itemsValue et dead.";
        if (Kills < 0 || Deaths < 0 || Assists < 0 || Cs < 0 || Vision < 0 || ItemsValue < 0)
            return "Les valeurs d'un joueur dans la chronologie ne sont pas négatives.";
        if (Level is < 1 or > ChronologieDeGame.NiveauMax)
            return $"Le niveau d'un joueur va de 1 à {ChronologieDeGame.NiveauMax}.";
        return null;
    }
}

/// <param name="Type">dragon, herald, baron, turret ou inhibitor.</param>
/// <param name="Side">Le side (« blue » ou « red ») de l'équipe qui a pris l'objectif.</param>
public sealed record EvenementDeChronologie(int? T, string? Type, string? Side);
