using Shameless.Application.Picker;

namespace Shameless.Application.Stats;

/// <summary>
/// Les mesures des axes du face-à-face qui ne sont pas des critères de la note (ADR 0011) : chacune a ses
/// références par rôle dans <see cref="ReferencesDImpact"/>.
/// </summary>
internal enum MesureDAxe
{
    /// <summary>Morts par minute ; moins vaut mieux (Faible dépasse Excellent).</summary>
    MortsParMinute,

    /// <summary>(Soins + boucliers posés sur les alliés) par minute ; demande les deux stats.</summary>
    SoinsEtBouclierParMinute,

    /// <summary>Secondes de contrôle de foule infligé, par minute.</summary>
    ControleParMinute,

    /// <summary>Tourelles + dragons + barons pris dans la game (pas par minute) ; demande les trois stats.</summary>
    ObjectifsPris,

    /// <summary>(Balises posées + balises détruites) par minute ; demande les deux stats.</summary>
    BalisesParMinute
}

/// <summary>
/// Les références, par rôle, des mesures propres au face-à-face (<see cref="MesureDAxe"/>). Mêmes règles que
/// la note : ≤ Faible vaut 0, = Moyen vaut 50, ≥ Excellent vaut 100, linéaire entre deux ; pour les morts,
/// l'échelle est inversée. Les autres mesures des axes (KDA, dégâts, part des dégâts, objectifs, vision,
/// or, CS) reprennent les références de <see cref="ReferencesDeNote"/>.
/// </summary>
/// <remarks>
/// À CALIBRER. Ces valeurs sont des ordres de grandeur initiaux, choisis à la main pour une partie
/// d'environ 25 à 30 minutes, sans aucune donnée de joueur ni de partie : ils n'ont pas été mesurés. La
/// procédure de calibration sur des percentiles de parties SoloQ (Faible = 10e, Moyen = 50e, Excellent = 90e
/// percentile du rôle) est dans <c>tools/note-calage/README.md</c> ; seuls les trois paramètres par rôle et
/// par mesure entrent dans le dépôt, jamais les parties ni les joueurs.
/// </remarks>
internal static class ReferencesDImpact
{
    private static ReferenceDeCritere R(double faible, double moyen, double excellent) => new(1, faible, moyen, excellent);

    private static readonly Dictionary<MesureDAxe, ReferenceDeCritere> Top = new()
    {
        [MesureDAxe.MortsParMinute] = R(0.40, 0.26, 0.12),
        [MesureDAxe.SoinsEtBouclierParMinute] = R(40, 150, 400),
        [MesureDAxe.ControleParMinute] = R(0.2, 0.6, 1.5),
        [MesureDAxe.ObjectifsPris] = R(0, 1, 3),
        [MesureDAxe.BalisesParMinute] = R(0.2, 0.4, 0.8)
    };

    private static readonly Dictionary<MesureDAxe, ReferenceDeCritere> Jungle = new()
    {
        [MesureDAxe.MortsParMinute] = R(0.40, 0.26, 0.12),
        [MesureDAxe.SoinsEtBouclierParMinute] = R(30, 110, 300),
        [MesureDAxe.ControleParMinute] = R(0.3, 0.8, 1.8),
        [MesureDAxe.ObjectifsPris] = R(0, 2, 4),
        [MesureDAxe.BalisesParMinute] = R(0.4, 0.7, 1.2)
    };

    private static readonly Dictionary<MesureDAxe, ReferenceDeCritere> Mid = new()
    {
        [MesureDAxe.MortsParMinute] = R(0.38, 0.25, 0.12),
        [MesureDAxe.SoinsEtBouclierParMinute] = R(30, 100, 300),
        [MesureDAxe.ControleParMinute] = R(0.2, 0.6, 1.5),
        [MesureDAxe.ObjectifsPris] = R(0, 1, 3),
        [MesureDAxe.BalisesParMinute] = R(0.25, 0.5, 0.9)
    };

    private static readonly Dictionary<MesureDAxe, ReferenceDeCritere> Adc = new()
    {
        [MesureDAxe.MortsParMinute] = R(0.38, 0.25, 0.12),
        [MesureDAxe.SoinsEtBouclierParMinute] = R(30, 100, 300),
        [MesureDAxe.ControleParMinute] = R(0.1, 0.3, 0.9),
        [MesureDAxe.ObjectifsPris] = R(0, 1, 3),
        [MesureDAxe.BalisesParMinute] = R(0.2, 0.4, 0.8)
    };

    private static readonly Dictionary<MesureDAxe, ReferenceDeCritere> Support = new()
    {
        [MesureDAxe.MortsParMinute] = R(0.42, 0.28, 0.14),
        [MesureDAxe.SoinsEtBouclierParMinute] = R(100, 300, 800),
        [MesureDAxe.ControleParMinute] = R(0.4, 1.0, 2.5),
        [MesureDAxe.ObjectifsPris] = R(0, 0.5, 2),
        [MesureDAxe.BalisesParMinute] = R(0.7, 1.1, 1.8)
    };

    /// <summary>Les références du rôle ; rôle inconnu : celles du Mid, comme la note.</summary>
    public static IReadOnlyDictionary<MesureDAxe, ReferenceDeCritere> Pour(Role? role) => role switch
    {
        Role.Top => Top,
        Role.Jungle => Jungle,
        Role.Adc => Adc,
        Role.Support => Support,
        _ => Mid
    };
}
