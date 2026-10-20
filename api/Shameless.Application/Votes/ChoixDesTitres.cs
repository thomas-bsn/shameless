namespace Shameless.Application.Votes;

/// <summary>
/// Le Joueur choisi pour le titre de DOFD, facultatif : le bulletin d'un votant ou la partie DOFD d'un
/// pronostic.
/// </summary>
public readonly record struct ChoixDesTitres(int? Dofd)
{
    /// <summary>Aucun Joueur n'est choisi.</summary>
    public bool Aucun => Dofd is null;

    /// <summary>Les choix faits, avec le nom de leur titre (« DOFD »).</summary>
    public IEnumerable<(int PlayerId, string Titre)> Faits()
    {
        if (Dofd is { } dofd) yield return (dofd, "DOFD");
    }

    /// <summary>Le titre du premier Joueur choisi hors de <paramref name="joueurs"/>, ou null s'ils en sont tous.</summary>
    public string? TitreHorsDe(IEnumerable<int> joueurs)
    {
        var admis = joueurs.ToHashSet();
        return Faits().Where(c => !admis.Contains(c.PlayerId)).Select(c => c.Titre).FirstOrDefault();
    }

    /// <summary>Le titre du premier Joueur choisi parmi <paramref name="joueurs"/>, ou null.</summary>
    public string? TitreParmi(IEnumerable<int> joueurs)
    {
        var exclus = joueurs.ToHashSet();
        return Faits().Where(c => exclus.Contains(c.PlayerId)).Select(c => c.Titre).FirstOrDefault();
    }
}

/// <summary>Le dépôt d'un bulletin ou d'un pronostic : le remplacer, ou le retirer quand il est vide.</summary>
public static class Depot
{
    /// <summary>
    /// Retire <paramref name="existant"/> si <paramref name="vide"/> ; sinon le crée au besoin (ajouté par
    /// <paramref name="ajouter"/>) puis le remplit.
    /// </summary>
    public static void RemplacerOuRetirer<T>(
        T? existant, bool vide, Func<T> creer, Action<T> ajouter, Action<T> retirer, Action<T> remplir)
        where T : class
    {
        if (vide)
        {
            if (existant != null) retirer(existant);
            return;
        }
        if (existant == null)
        {
            existant = creer();
            ajouter(existant);
        }
        remplir(existant);
    }
}
