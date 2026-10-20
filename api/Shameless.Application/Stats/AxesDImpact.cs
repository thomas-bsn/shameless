using Shameless.Application.Picker;

namespace Shameless.Application.Stats;

/// <summary>
/// Les six axes du face-à-face d'un participant, chacun de 0 à 100 : la moyenne des scores des mesures de
/// l'axe qu'on peut calculer, notées par rapport aux références du rôle (note : <see cref="ReferencesDeNote"/> ;
/// autres mesures : <see cref="ReferencesDImpact"/>, à calibrer). Un axe sans aucune mesure possible est absent.
/// </summary>
internal static class AxesDImpact
{
    /// <summary>Une mesure d'axe : un critère de la note, ou une mesure propre au face-à-face.</summary>
    private readonly record struct Source(CritereDeNote? Critere, MesureDAxe? Mesure);

    private static Source Note(CritereDeNote c) => new(c, null);
    private static Source Propre(MesureDAxe m) => new(null, m);

    private static readonly (AxeDImpact Axe, Source[] Sources)[] Definition =
    [
        (AxeDImpact.Survie, [Propre(MesureDAxe.MortsParMinute)]),
        (AxeDImpact.Utilite, [Propre(MesureDAxe.SoinsEtBouclierParMinute), Propre(MesureDAxe.ControleParMinute)]),
        (AxeDImpact.Combat, [Note(CritereDeNote.Kda), Note(CritereDeNote.DegatsParMinute), Note(CritereDeNote.PartDesDegats)]),
        (AxeDImpact.Objectifs, [Note(CritereDeNote.ObjectifsParMinute), Propre(MesureDAxe.ObjectifsPris)]),
        (AxeDImpact.Vision, [Note(CritereDeNote.VisionParMinute), Propre(MesureDAxe.BalisesParMinute)]),
        (AxeDImpact.Economie, [Note(CritereDeNote.OrParMinute), Note(CritereDeNote.CsParMinute)])
    ];

    /// <summary>Le score de chaque axe mesurable du participant, dans l'ordre de <see cref="AxeDImpact"/>.</summary>
    public static IReadOnlyDictionary<AxeDImpact, double> Scores(
        Role? role, StatsDuParticipant? stats, NotesDeLaGame.TotauxDEquipe equipe, int? dureeSecondes)
    {
        var scores = new Dictionary<AxeDImpact, double>();
        if (stats is null) return scores;

        var mesuresDeNote = NoteDePerformance.Mesurer(stats, equipe.Kills, equipe.Degats, equipe.Or, dureeSecondes);
        var mesuresPropres = MesuresPropres(stats, dureeSecondes);
        var referencesDeNote = ReferencesDeNote.Pour(role).Criteres;
        var referencesPropres = ReferencesDImpact.Pour(role);

        double? ScoreDe(Source s) => s switch
        {
            { Critere: { } c } when mesuresDeNote.TryGetValue(c, out var v) && referencesDeNote.TryGetValue(c, out var r) => NoteDePerformance.Score(v, r),
            { Mesure: { } m } when mesuresPropres.TryGetValue(m, out var v) => NoteDePerformance.Score(v, referencesPropres[m]),
            _ => null
        };

        foreach (var (axe, sources) in Definition)
        {
            var mesures = sources.Select(ScoreDe).OfType<double>().ToList();
            if (mesures.Count > 0) scores[axe] = mesures.Average();
        }
        return scores;
    }

    /// <summary>La valeur de chaque mesure propre au face-à-face ; absente si une de ses stats ou la durée manque.</summary>
    private static Dictionary<MesureDAxe, double> MesuresPropres(StatsDuParticipant s, int? dureeSecondes)
    {
        var mesures = new Dictionary<MesureDAxe, double>();
        if (s is { TurretKills: { } tourelles, DragonKills: { } dragons, BaronKills: { } barons })
            mesures[MesureDAxe.ObjectifsPris] = tourelles + dragons + barons;
        if (dureeSecondes is not > 0) return mesures;

        var minutes = dureeSecondes.Value / 60.0;
        if (s.Deaths is { } morts) mesures[MesureDAxe.MortsParMinute] = morts / minutes;
        if (s is { TotalHeal: { } soins, DamageShielded: { } boucliers }) mesures[MesureDAxe.SoinsEtBouclierParMinute] = (soins + boucliers) / minutes;
        if (s.TimeCcingOthers is { } controle) mesures[MesureDAxe.ControleParMinute] = controle / minutes;
        if (s is { WardsPlaced: { } posees, WardsKilled: { } detruites }) mesures[MesureDAxe.BalisesParMinute] = (posees + detruites) / minutes;
        return mesures;
    }

    /// <summary>
    /// Le face-à-face de <paramref name="joueur"/> contre <paramref name="adversaire"/>, sur les axes mesurés des deux
    /// côtés ; null si aucun ne l'est.
    /// </summary>
    public static FaceAFace? Comparer(
        int adversaireId, Role? role, IReadOnlyDictionary<AxeDImpact, double> joueur, IReadOnlyDictionary<AxeDImpact, double> adversaire)
    {
        var axes = Enum.GetValues<AxeDImpact>()
            .Where(a => joueur.ContainsKey(a) && adversaire.ContainsKey(a))
            .Select(a =>
            {
                int moi = Arrondi(joueur[a]), lui = Arrondi(adversaire[a]);
                return new AxeDeFaceAFace(a, moi, lui, moi - lui);
            })
            .ToList();
        return axes.Count == 0 ? null
            : new FaceAFace(adversaireId, role, axes, axes.Count(a => a.Ecart > 0), axes.Count(a => a.Ecart < 0));
    }

    private static int Arrondi(double score) => (int)Math.Round(score, MidpointRounding.AwayFromZero);
}
