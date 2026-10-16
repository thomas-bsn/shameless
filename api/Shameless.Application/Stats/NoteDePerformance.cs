using Shameless.Application.Picker;

namespace Shameless.Application.Stats;

/// <summary>Un critère noté dans la note de performance.</summary>
/// <param name="Valeur">La valeur mesurée dans la game (ex. 7,2 CS/min), arrondie à 2 décimales.</param>
/// <param name="Score">Son score de 0 à 100 par rapport aux références du rôle.</param>
/// <param name="Poids">Sa part dans la note, entre 0 et 1, après renormalisation sur les critères disponibles.</param>
public sealed record ComposanteDeNote(CritereDeNote Critere, double Valeur, int Score, double Poids);

/// <summary>
/// La note Shameless de performance d'un Joueur pour une Game, de 0 à 100 : la moyenne des scores
/// de ses critères, pondérés selon le rôle joué. Les critères dont les stats manquent sont omis et
/// les poids restants renormalisés. Formule et références : ADR 0011 et <see cref="ReferencesDeNote"/>.
/// </summary>
/// <param name="Role">Le rôle selon lequel le Joueur est noté, null si inconnu (profil polyvalent).</param>
/// <param name="SourceDuRole">D'où vient ce rôle.</param>
/// <param name="Composantes">Les critères notés, dans l'ordre de <see cref="CritereDeNote"/>.</param>
public sealed record NoteDePerformance(int Note, Role? Role, SourceDuRole SourceDuRole, IReadOnlyList<ComposanteDeNote> Composantes)
{
    /// <summary>
    /// La note d'un Joueur à partir de ses stats, de son rôle, des kills de son Équipe et de la durée
    /// de la game ; null si aucun critère du rôle n'est mesurable.
    /// </summary>
    public static NoteDePerformance? Calculer(StatsDuParticipant stats, RoleJoue role, int? killsDeLEquipe, int? dureeSecondes)
    {
        var mesures = Mesurer(stats, killsDeLEquipe, dureeSecondes);
        var notes = ReferencesDeNote.Pour(role.Role)
            .Where(r => r.Value.Poids > 0 && mesures.ContainsKey(r.Key))
            .OrderBy(r => r.Key)
            .Select(r => (Critere: r.Key, Valeur: mesures[r.Key], Score: Score(mesures[r.Key], r.Value), r.Value.Poids))
            .ToList();
        if (notes.Count == 0) return null;

        double poidsTotal = notes.Sum(n => n.Poids);
        var note = notes.Sum(n => n.Score * n.Poids) / poidsTotal;
        return new NoteDePerformance(
            Arrondi(note),
            role.Role,
            role.Source,
            notes.Select(n => new ComposanteDeNote(n.Critere, Math.Round(n.Valeur, 2), Arrondi(n.Score), Math.Round(n.Poids / poidsTotal, 3))).ToList());
    }

    /// <summary>La valeur de chaque critère mesurable ; un critère manque si une de ses stats, la durée ou les kills de l'Équipe manquent.</summary>
    private static Dictionary<CritereDeNote, double> Mesurer(StatsDuParticipant s, int? killsDeLEquipe, int? dureeSecondes)
    {
        var mesures = new Dictionary<CritereDeNote, double>();
        if (s is { Kills: { } k, Deaths: { } d, Assists: { } a })
            mesures[CritereDeNote.Kda] = (double)(k + a) / Math.Max(1, d);
        if (s is { Kills: { } kills, Assists: { } assists } && killsDeLEquipe > 0)
            mesures[CritereDeNote.ParticipationAuxKills] = Math.Min(1.0, (double)(kills + assists) / killsDeLEquipe.Value);

        if (dureeSecondes is not > 0) return mesures;
        var minutes = dureeSecondes.Value / 60.0;
        if (s.MinionsKilled is not null || s.NeutralMinionsKilled is not null)
            mesures[CritereDeNote.CsParMinute] = ((s.MinionsKilled ?? 0) + (s.NeutralMinionsKilled ?? 0)) / minutes;
        if (s.DamageToChampions is { } degats) mesures[CritereDeNote.DegatsParMinute] = degats / minutes;
        if (s.VisionScore is { } vision) mesures[CritereDeNote.VisionParMinute] = vision / minutes;
        if (s.DamageToObjectives is { } objectifs) mesures[CritereDeNote.ObjectifsParMinute] = objectifs / minutes;
        return mesures;
    }

    /// <summary>0 jusqu'à Faible, 50 à Moyen, 100 dès Excellent ; linéaire entre deux références.</summary>
    private static double Score(double valeur, ReferenceDeCritere r)
    {
        if (valeur <= r.Faible) return 0;
        if (valeur >= r.Excellent) return 100;
        return valeur < r.Moyen
            ? 50 * (valeur - r.Faible) / (r.Moyen - r.Faible)
            : 50 + 50 * (valeur - r.Moyen) / (r.Excellent - r.Moyen);
    }

    private static int Arrondi(double score) => (int)Math.Round(score, MidpointRounding.AwayFromZero);
}
