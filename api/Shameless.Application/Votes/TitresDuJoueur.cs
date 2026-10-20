namespace Shameless.Application.Votes;

/// <summary>Le nombre de titres de DOFD remportés par un Joueur, égalités comprises.</summary>
public sealed record TitresDuJoueur(int Dofd)
{
    public static readonly TitresDuJoueur Aucun = new(0);

    /// <summary>Les titres de chaque Joueur d'après les résultats donnés : seuls les votes clos comptent.</summary>
    public static Dictionary<int, TitresDuJoueur> Compter(IEnumerable<ResultatsDesVotes> resultats)
    {
        var titres = new Dictionary<int, TitresDuJoueur>();
        foreach (var resultat in resultats.Where(r => r.Statut == FenetreDeVote.Clos))
        {
            foreach (var playerId in resultat.Dofd.Gagnants)
                titres[playerId] = titres.GetValueOrDefault(playerId, Aucun) with { Dofd = titres.GetValueOrDefault(playerId, Aucun).Dofd + 1 };
        }
        return titres;
    }
}
