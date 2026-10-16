namespace Shameless.Application.Stats;

/// <summary>
/// Les formules des stats de base, communes à la note de performance d'une Game et aux stats
/// agrégées d'un Joueur (fiche) : appliquées aux valeurs d'une Game ou à leurs totaux.
/// </summary>
public static class FormulesDeStats
{
    /// <summary>(kills + assists) / morts, au moins 1 mort pour ne pas diviser par zéro.</summary>
    public static double Kda(int kills, int morts, int assists) => (double)(kills + assists) / Math.Max(1, morts);

    /// <summary>(kills + assists) / kills de l'Équipe, en fraction (non plafonnée) ; l'Équipe doit avoir au moins un kill.</summary>
    public static double ParticipationAuxKills(int kills, int assists, int killsDeLEquipe) =>
        (double)(kills + assists) / killsDeLEquipe;

    /// <summary>Le CS : sbires et monstres neutres tués ; null si aucun des deux n'est connu.</summary>
    public static int? Cs(StatsDuParticipant stats) =>
        stats.MinionsKilled is null && stats.NeutralMinionsKilled is null
            ? null
            : (stats.MinionsKilled ?? 0) + (stats.NeutralMinionsKilled ?? 0);
}
