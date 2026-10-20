using Shameless.Application.Picker;

namespace Shameless.Application.Stats;

/// <summary>Un critère de la note de performance, mesuré dans les stats d'une Game, ou la victoire.</summary>
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
    ObjectifsParMinute,

    /// <summary>Or gagné par minute.</summary>
    OrParMinute,

    /// <summary>Part des kills de l'Équipe faits par le Joueur (kills / kills de l'Équipe), entre 0 et 1.</summary>
    PartDesKills,

    /// <summary>Part des dégâts de l'Équipe aux champions infligés par le Joueur, entre 0 et 1.</summary>
    PartDesDegats,

    /// <summary>Part de l'or de l'Équipe gagné par le Joueur, entre 0 et 1.</summary>
    PartDeLOr,

    /// <summary>Le résultat de la game : valeur 1 et score 100 si l'Équipe du Joueur a gagné, 0 et 0 sinon.</summary>
    Victoire
}

/// <summary>
/// Comment un critère est noté pour un rôle : son poids dans la note, et les trois valeurs de
/// référence du rôle. Une valeur inférieure ou égale à <paramref name="Faible"/> vaut 0, égale à
/// <paramref name="Moyen"/> vaut 50, supérieure ou égale à <paramref name="Excellent"/> vaut 100 ;
/// entre deux références, le score est linéaire.
/// </summary>
public sealed record ReferenceDeCritere(int Poids, double Faible, double Moyen, double Excellent);

/// <summary>
/// Ce qui note un rôle : ses critères mesurés (poids et références) et le poids de la composante
/// victoire (score 100 si la game est gagnée, 0 sinon). Les poids sont relatifs : seule leur
/// proportion compte.
/// </summary>
public sealed record ReferencesDuRole(IReadOnlyDictionary<CritereDeNote, ReferenceDeCritere> Criteres, int PoidsVictoire);

/// <summary>
/// Les pondérations et références de la note de performance, par rôle : le seul endroit où elles
/// sont fixées (ADR 0011). Les références sont des repères de performance du rôle en partie
/// classique (SoloQ), pas une comparaison entre Joueurs de l'Instance, dont l'échantillon est trop
/// réduit.
/// </summary>
/// <remarks>
/// Valeurs calées sur les scores de dpm.lol (743 lignes de SoloQ, cinq rôles) puis lissées : voir
/// <c>tools/note-calage/resultats/calage-2026-10-20-lisse.json</c> (valeurs, règle de lissage et
/// écarts mesurés) et son README. Pour changer une valeur, relancer l'outil de calage
/// (<c>tools/note-calage</c>) et reporter ses résultats ici ; ne pas retoucher à la main.
/// Le CS du support n'est pas noté : ce n'est pas son rôle.
/// </remarks>
public static class ReferencesDeNote
{
    private static readonly ReferencesDuRole Top = new(PoidsVictoire: 11, Criteres: new Dictionary<CritereDeNote, ReferenceDeCritere>
    {
        [CritereDeNote.Kda] = new(32, Faible: 0.332, Moyen: 1.02, Excellent: 2.78),
        [CritereDeNote.ParticipationAuxKills] = new(1, Faible: 0.092, Moyen: 0.291, Excellent: 0.508),
        [CritereDeNote.CsParMinute] = new(12, Faible: 5.55, Moyen: 7.19, Excellent: 8.27),
        [CritereDeNote.DegatsParMinute] = new(11, Faible: 0, Moyen: 824, Excellent: 947),
        [CritereDeNote.VisionParMinute] = new(4, Faible: 0.283, Moyen: 0.333, Excellent: 0.737),
        [CritereDeNote.ObjectifsParMinute] = new(11, Faible: 130, Moyen: 367, Excellent: 600),
        [CritereDeNote.OrParMinute] = new(11, Faible: 359, Moyen: 423, Excellent: 486),
        [CritereDeNote.PartDesKills] = new(1, Faible: 0.08, Moyen: 0.2, Excellent: 0.32),
        [CritereDeNote.PartDesDegats] = new(5, Faible: 0.169, Moyen: 0.199, Excellent: 0.323),
        [CritereDeNote.PartDeLOr] = new(1, Faible: 0.14, Moyen: 0.21, Excellent: 0.28),
    });

    private static readonly ReferencesDuRole Jungle = new(PoidsVictoire: 8, Criteres: new Dictionary<CritereDeNote, ReferenceDeCritere>
    {
        [CritereDeNote.Kda] = new(21, Faible: 1.33, Moyen: 3.5, Excellent: 5.64),
        [CritereDeNote.ParticipationAuxKills] = new(7, Faible: 0.4, Moyen: 0.469, Excellent: 0.615),
        [CritereDeNote.CsParMinute] = new(9, Faible: 5.14, Moyen: 5.84, Excellent: 7.4),
        [CritereDeNote.DegatsParMinute] = new(4, Faible: 454, Moyen: 516, Excellent: 939),
        [CritereDeNote.VisionParMinute] = new(9, Faible: 0.458, Moyen: 0.982, Excellent: 1.1),
        [CritereDeNote.ObjectifsParMinute] = new(17, Faible: 305, Moyen: 621, Excellent: 870),
        [CritereDeNote.OrParMinute] = new(13, Faible: 355, Moyen: 461, Excellent: 517),
        [CritereDeNote.PartDesKills] = new(11, Faible: 0.058, Moyen: 0.066, Excellent: 0.269),
        [CritereDeNote.PartDesDegats] = new(1, Faible: 0.08, Moyen: 0.16, Excellent: 0.24),
        [CritereDeNote.PartDeLOr] = new(1, Faible: 0.171, Moyen: 0.214, Excellent: 0.24),
    });

    private static readonly ReferencesDuRole Mid = new(PoidsVictoire: 15, Criteres: new Dictionary<CritereDeNote, ReferenceDeCritere>
    {
        [CritereDeNote.Kda] = new(23, Faible: 1.13, Moyen: 1.53, Excellent: 3.51),
        [CritereDeNote.ParticipationAuxKills] = new(1, Faible: 0.196, Moyen: 0.435, Excellent: 0.619),
        [CritereDeNote.CsParMinute] = new(9, Faible: 5.16, Moyen: 6.13, Excellent: 7.45),
        [CritereDeNote.DegatsParMinute] = new(20, Faible: 63, Moyen: 605, Excellent: 952),
        [CritereDeNote.VisionParMinute] = new(4, Faible: 0, Moyen: 0.851, Excellent: 0.979),
        [CritereDeNote.ObjectifsParMinute] = new(12, Faible: 103, Moyen: 248, Excellent: 381),
        [CritereDeNote.OrParMinute] = new(11, Faible: 286, Moyen: 528, Excellent: 607),
        [CritereDeNote.PartDesKills] = new(4, Faible: 0, Moyen: 0.305, Excellent: 0.35),
        [CritereDeNote.PartDesDegats] = new(1, Faible: 0.14, Moyen: 0.24, Excellent: 0.34),
        [CritereDeNote.PartDeLOr] = new(1, Faible: 0.16, Moyen: 0.22, Excellent: 0.29),
    });

    private static readonly ReferencesDuRole Adc = new(PoidsVictoire: 7, Criteres: new Dictionary<CritereDeNote, ReferenceDeCritere>
    {
        [CritereDeNote.Kda] = new(25, Faible: 0.714, Moyen: 2.43, Excellent: 3.3),
        [CritereDeNote.ParticipationAuxKills] = new(1, Faible: 0.219, Moyen: 0.427, Excellent: 0.626),
        [CritereDeNote.CsParMinute] = new(14, Faible: 3.24, Moyen: 7.6, Excellent: 8.74),
        [CritereDeNote.DegatsParMinute] = new(12, Faible: 442, Moyen: 621, Excellent: 1043),
        [CritereDeNote.VisionParMinute] = new(7, Faible: 0.285, Moyen: 0.349, Excellent: 0.902),
        [CritereDeNote.ObjectifsParMinute] = new(18, Faible: 20, Moyen: 121, Excellent: 677),
        [CritereDeNote.OrParMinute] = new(13, Faible: 385, Moyen: 491, Excellent: 564),
        [CritereDeNote.PartDesKills] = new(1, Faible: 0.1, Moyen: 0.22, Excellent: 0.34),
        [CritereDeNote.PartDesDegats] = new(1, Faible: 0.16, Moyen: 0.26, Excellent: 0.36),
        [CritereDeNote.PartDeLOr] = new(1, Faible: 0.17, Moyen: 0.23, Excellent: 0.3),
    });

    private static readonly ReferencesDuRole Support = new(PoidsVictoire: 12, Criteres: new Dictionary<CritereDeNote, ReferenceDeCritere>
    {
        [CritereDeNote.Kda] = new(23, Faible: 1.24, Moyen: 1.91, Excellent: 4.86),
        [CritereDeNote.ParticipationAuxKills] = new(10, Faible: 0.332, Moyen: 0.39, Excellent: 0.667),
        [CritereDeNote.DegatsParMinute] = new(11, Faible: 159, Moyen: 187, Excellent: 1064),
        [CritereDeNote.VisionParMinute] = new(21, Faible: 0, Moyen: 2, Excellent: 2.91),
        [CritereDeNote.ObjectifsParMinute] = new(4, Faible: 24, Moyen: 29, Excellent: 114),
        [CritereDeNote.OrParMinute] = new(17, Faible: 263, Moyen: 335, Excellent: 385),
        [CritereDeNote.PartDesKills] = new(1, Faible: 0.02, Moyen: 0.08, Excellent: 0.14),
        [CritereDeNote.PartDesDegats] = new(1, Faible: 0.042, Moyen: 0.085, Excellent: 0.169),
        [CritereDeNote.PartDeLOr] = new(1, Faible: 0.07, Moyen: 0.09, Excellent: 0.118),
    });

    /// <summary>Rôle inconnu : les références du mid, tous les critères et la victoire au même poids.</summary>
    private static readonly ReferencesDuRole Polyvalent = new(
        Mid.Criteres.ToDictionary(c => c.Key, c => c.Value with { Poids = 1 }), PoidsVictoire: 1);

    /// <summary>Ce qui note un rôle (null = rôle inconnu).</summary>
    public static ReferencesDuRole Pour(Role? role) => role switch
    {
        Role.Top => Top,
        Role.Jungle => Jungle,
        Role.Mid => Mid,
        Role.Adc => Adc,
        Role.Support => Support,
        _ => Polyvalent
    };
}
