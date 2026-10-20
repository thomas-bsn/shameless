using System.Text.Json.Serialization;
using Shameless.Application.Entities;
using Shameless.Application.Picker;

namespace Shameless.Application.Stats;

/// <summary>La lettre d'un score d'Impact : S+ ≥ 90, S ≥ 80, A ≥ 70, B ≥ 55, C ≥ 40, sinon D.</summary>
public enum LettreDImpact
{
    [JsonStringEnumMemberName("S+")] SPlus,
    S,
    A,
    B,
    C,
    D
}

/// <summary>Les groupes de sous-scores du panneau Impact. Un critère de la note peut figurer dans plusieurs groupes.</summary>
public enum GroupeDImpact
{
    /// <summary>Tous les critères de la note : son score et sa lettre sont ceux de la note.</summary>
    Global,

    /// <summary>Ce qui se joue en face-à-face : KDA, CS, dégâts et or par minute.</summary>
    VsAdversaire,

    /// <summary>Les dégâts aux objectifs par minute.</summary>
    Objectifs,

    /// <summary>Le rapport à l'Équipe : participation, parts de kills, de dégâts et d'or, et la victoire.</summary>
    Equipe,

    /// <summary>Les trois critères les plus pesants pour le rôle joué (victoire exclue) ; absent si le rôle est inconnu.</summary>
    Role
}

/// <summary>Une ligne d'un groupe : un critère de la note, avec ce qu'il rapporte.</summary>
/// <param name="Valeur">La valeur mesurée (victoire : 1 ou 0).</param>
/// <param name="Score">Son score de 0 à 100 par rapport aux références du rôle.</param>
/// <param name="Poids">Son poids dans la note, entre 0 et 1, renormalisé sur les critères mesurés.</param>
/// <param name="Points">Ce qu'il apporte à la note : score × poids, à une décimale.</param>
public sealed record LigneDImpact(CritereDeNote Critere, double Valeur, int Score, double Poids, double Points);

/// <summary>Un groupe de sous-scores : son score (moyenne des lignes pondérée par leur poids), sa lettre et ses lignes.</summary>
/// <param name="Points">La somme des points de ses lignes.</param>
public sealed record SousScoreDImpact(GroupeDImpact Groupe, int Score, LettreDImpact Lettre, double Points, IReadOnlyList<LigneDImpact> Lignes);

/// <summary>Les six axes du face-à-face.</summary>
public enum AxeDImpact
{
    Survie,
    Utilite,
    Combat,
    Objectifs,
    Vision,
    Economie
}

/// <summary>Un axe mesuré pour le Joueur et pour son adversaire ; <paramref name="Ecart"/> = score − score de l'adversaire.</summary>
public sealed record AxeDeFaceAFace(AxeDImpact Axe, int Score, int ScoreAdversaire, int Ecart);

/// <summary>Le face-à-face avec l'adversaire du même rôle : ses axes mesurés des deux côtés, et le décompte des axes gagnés et perdus (une égalité ne compte pour aucun).</summary>
public sealed record FaceAFace(int AdversaireId, Role? Role, IReadOnlyList<AxeDeFaceAFace> Axes, int AxesGagnes, int AxesPerdus);

/// <summary>
/// Ce que le panneau Impact d'un Joueur montre pour une Game : ses sous-scores par groupe, tirés des
/// composantes de la note (ADR 0011), et son face-à-face. Une stat absente masque sa ligne ou son axe.
/// </summary>
/// <param name="SousScores">Vide sans note ; les groupes sans ligne sont omis.</param>
/// <param name="FaceAFace">Null sans adversaire unique du même rôle, ou sans axe mesuré des deux côtés.</param>
public sealed record ImpactDuJoueur(IReadOnlyList<SousScoreDImpact> SousScores, FaceAFace? FaceAFace)
{
    private const int NombreDeCriteresDuRole = 3;

    private static readonly CritereDeNote[] VsAdversaire =
        [CritereDeNote.Kda, CritereDeNote.CsParMinute, CritereDeNote.DegatsParMinute, CritereDeNote.OrParMinute];

    private static readonly CritereDeNote[] Equipe =
        [CritereDeNote.ParticipationAuxKills, CritereDeNote.PartDesKills, CritereDeNote.PartDesDegats, CritereDeNote.PartDeLOr, CritereDeNote.Victoire];

    public static LettreDImpact Lettre(int score) => score switch
    {
        >= 90 => LettreDImpact.SPlus,
        >= 80 => LettreDImpact.S,
        >= 70 => LettreDImpact.A,
        >= 55 => LettreDImpact.B,
        >= 40 => LettreDImpact.C,
        _ => LettreDImpact.D
    };

    /// <summary>Les sous-scores d'une note ; vide sans note.</summary>
    internal static IReadOnlyList<SousScoreDImpact> SousScoresDe(NoteDePerformance? note)
    {
        if (note is null) return [];
        var lignes = note.Composantes
            .Select(c => new LigneDImpact(c.Critere, c.Valeur, c.Score, c.Poids, Math.Round(c.Score * c.Poids, 1)))
            .ToList();

        var groupes = new List<SousScoreDImpact> { new(GroupeDImpact.Global, note.Note, Lettre(note.Note), Points(lignes), lignes) };
        void Ajouter(GroupeDImpact groupe, IEnumerable<LigneDImpact> choisies)
        {
            var l = choisies.ToList();
            if (l.Count == 0) return;
            var score = (int)Math.Round(l.Sum(x => x.Score * x.Poids) / l.Sum(x => x.Poids), MidpointRounding.AwayFromZero);
            groupes.Add(new SousScoreDImpact(groupe, score, Lettre(score), Points(l), l));
        }

        Ajouter(GroupeDImpact.VsAdversaire, lignes.Where(l => VsAdversaire.Contains(l.Critere)));
        Ajouter(GroupeDImpact.Objectifs, lignes.Where(l => l.Critere == CritereDeNote.ObjectifsParMinute));
        Ajouter(GroupeDImpact.Equipe, lignes.Where(l => Equipe.Contains(l.Critere)));
        if (note.Role is { } role)
        {
            var lourds = ReferencesDeNote.Pour(role).Criteres
                .OrderByDescending(r => r.Value.Poids).ThenBy(r => r.Key)
                .Select(r => r.Key).Where(c => lignes.Any(l => l.Critere == c)).Take(NombreDeCriteresDuRole).ToHashSet();
            Ajouter(GroupeDImpact.Role, lignes.Where(l => lourds.Contains(l.Critere)));
        }
        return groupes;
    }

    private static double Points(IReadOnlyList<LigneDImpact> lignes) => Math.Round(lignes.Sum(l => l.Score * l.Poids), 1);
}
