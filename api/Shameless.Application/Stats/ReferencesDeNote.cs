using Shameless.Application.Picker;

namespace Shameless.Application.Stats;

/// <summary>Un critère de la note de performance, mesuré dans les stats d'une Game.</summary>
public enum CritereDeNote
{
    /// <summary>(kills + assists) / max(1, morts).</summary>
    Kda,

    /// <summary>Participation aux kills : (kills + assists) / kills de l'Équipe, entre 0 et 1.</summary>
    ParticipationAuxKills,

    /// <summary>(sbires + monstres neutres) par minute.</summary>
    CsParMinute,

    /// <summary>Dégâts infligés aux champions par minute.</summary>
    DegatsParMinute,

    /// <summary>Score de vision par minute.</summary>
    VisionParMinute,

    /// <summary>Dégâts aux objectifs (tourelles, dragons, barons…) par minute.</summary>
    ObjectifsParMinute
}

/// <summary>
/// Comment un critère est noté pour un rôle : son poids dans la note, et les trois valeurs de
/// référence du rôle. Une valeur inférieure ou égale à <paramref name="Faible"/> vaut 0, égale à
/// <paramref name="Moyen"/> vaut 50, supérieure ou égale à <paramref name="Excellent"/> vaut 100 ;
/// entre deux références, le score est linéaire.
/// </summary>
public sealed record ReferenceDeCritere(int Poids, double Faible, double Moyen, double Excellent);

/// <summary>
/// Les pondérations et références de la note de performance, par rôle : le seul endroit où elles
/// sont fixées (ADR 0011). Les références sont des repères de performance du rôle en partie
/// classique (ordres de grandeur SoloQ), pas une comparaison entre Joueurs de l'Instance, dont
/// l'échantillon est trop réduit. Valeurs initiales, à ajuster avec les games réelles.
/// </summary>
public static class ReferencesDeNote
{
    private static readonly IReadOnlyDictionary<CritereDeNote, ReferenceDeCritere> Top = new Dictionary<CritereDeNote, ReferenceDeCritere>
    {
        [CritereDeNote.Kda] = new(20, Faible: 1.0, Moyen: 2.5, Excellent: 5.0),
        [CritereDeNote.ParticipationAuxKills] = new(15, Faible: 0.20, Moyen: 0.40, Excellent: 0.60),
        [CritereDeNote.CsParMinute] = new(20, Faible: 4.5, Moyen: 6.5, Excellent: 8.5),
        [CritereDeNote.DegatsParMinute] = new(20, Faible: 300, Moyen: 600, Excellent: 900),
        [CritereDeNote.VisionParMinute] = new(10, Faible: 0.3, Moyen: 0.6, Excellent: 1.0),
        [CritereDeNote.ObjectifsParMinute] = new(15, Faible: 200, Moyen: 450, Excellent: 800),
    };

    private static readonly IReadOnlyDictionary<CritereDeNote, ReferenceDeCritere> Jungle = new Dictionary<CritereDeNote, ReferenceDeCritere>
    {
        [CritereDeNote.Kda] = new(20, Faible: 1.0, Moyen: 3.0, Excellent: 6.0),
        [CritereDeNote.ParticipationAuxKills] = new(25, Faible: 0.35, Moyen: 0.60, Excellent: 0.80),
        [CritereDeNote.CsParMinute] = new(10, Faible: 3.5, Moyen: 5.5, Excellent: 7.0),
        [CritereDeNote.DegatsParMinute] = new(10, Faible: 250, Moyen: 450, Excellent: 700),
        [CritereDeNote.VisionParMinute] = new(15, Faible: 0.5, Moyen: 1.0, Excellent: 1.5),
        [CritereDeNote.ObjectifsParMinute] = new(20, Faible: 300, Moyen: 600, Excellent: 1000),
    };

    private static readonly IReadOnlyDictionary<CritereDeNote, ReferenceDeCritere> Mid = new Dictionary<CritereDeNote, ReferenceDeCritere>
    {
        [CritereDeNote.Kda] = new(20, Faible: 1.0, Moyen: 3.0, Excellent: 6.0),
        [CritereDeNote.ParticipationAuxKills] = new(20, Faible: 0.30, Moyen: 0.50, Excellent: 0.70),
        [CritereDeNote.CsParMinute] = new(20, Faible: 5.0, Moyen: 7.0, Excellent: 9.0),
        [CritereDeNote.DegatsParMinute] = new(25, Faible: 400, Moyen: 700, Excellent: 1000),
        [CritereDeNote.VisionParMinute] = new(5, Faible: 0.4, Moyen: 0.8, Excellent: 1.2),
        [CritereDeNote.ObjectifsParMinute] = new(10, Faible: 100, Moyen: 300, Excellent: 600),
    };

    private static readonly IReadOnlyDictionary<CritereDeNote, ReferenceDeCritere> Adc = new Dictionary<CritereDeNote, ReferenceDeCritere>
    {
        [CritereDeNote.Kda] = new(20, Faible: 1.0, Moyen: 3.0, Excellent: 6.0),
        [CritereDeNote.ParticipationAuxKills] = new(15, Faible: 0.30, Moyen: 0.50, Excellent: 0.70),
        [CritereDeNote.CsParMinute] = new(25, Faible: 5.5, Moyen: 7.5, Excellent: 9.5),
        [CritereDeNote.DegatsParMinute] = new(25, Faible: 400, Moyen: 750, Excellent: 1100),
        [CritereDeNote.VisionParMinute] = new(5, Faible: 0.3, Moyen: 0.6, Excellent: 1.0),
        [CritereDeNote.ObjectifsParMinute] = new(10, Faible: 200, Moyen: 500, Excellent: 900),
    };

    /// <summary>Le support n'est pas noté sur le CS ni les objectifs : ce n'est pas son rôle.</summary>
    private static readonly IReadOnlyDictionary<CritereDeNote, ReferenceDeCritere> Support = new Dictionary<CritereDeNote, ReferenceDeCritere>
    {
        [CritereDeNote.Kda] = new(25, Faible: 1.5, Moyen: 3.5, Excellent: 7.0),
        [CritereDeNote.ParticipationAuxKills] = new(30, Faible: 0.40, Moyen: 0.60, Excellent: 0.80),
        [CritereDeNote.DegatsParMinute] = new(10, Faible: 150, Moyen: 300, Excellent: 500),
        [CritereDeNote.VisionParMinute] = new(35, Faible: 1.0, Moyen: 2.0, Excellent: 3.0),
    };

    /// <summary>Rôle inconnu : les références du mid, tous les critères au même poids.</summary>
    private static readonly IReadOnlyDictionary<CritereDeNote, ReferenceDeCritere> Polyvalent =
        Mid.ToDictionary(c => c.Key, c => c.Value with { Poids = 1 });

    /// <summary>Les critères notés pour un rôle (null = rôle inconnu), avec leur poids et leurs références.</summary>
    public static IReadOnlyDictionary<CritereDeNote, ReferenceDeCritere> Pour(Role? role) => role switch
    {
        Role.Top => Top,
        Role.Jungle => Jungle,
        Role.Mid => Mid,
        Role.Adc => Adc,
        Role.Support => Support,
        _ => Polyvalent
    };
}
