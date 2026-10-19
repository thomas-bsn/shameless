using Shameless.Application.Entities;
using Shameless.Application.Joueurs;

namespace Shameless.Application.Classements;

/// <summary>
/// Le classement SoloQ de l'Instance : les rangs Riot actuels, une liste par file classée (SoloQ et Flex).
/// Distinct du classement 5v5 ; le Peak n'y entre pas.
/// </summary>
public record ClassementSoloQ(List<LigneClassementSoloQ> Solo, List<LigneClassementSoloQ> Flex)
{
    /// <summary>
    /// Classe les Joueurs du meilleur au moins bon rang dans chaque file. Un Joueur non classé dans une file
    /// garde sa ligne (rang null), en fin de liste, par prénom.
    /// </summary>
    public static ClassementSoloQ Calculer(IReadOnlyCollection<Player> joueurs) => new(
        Classer(joueurs, j => RangActuel.De(j.RankTier, j.RankDivision, j.LP)),
        Classer(joueurs, j => RangActuel.De(j.FlexRankTier, j.FlexRankDivision, j.FlexLp)));

    private static List<LigneClassementSoloQ> Classer(IEnumerable<Player> joueurs, Func<Player, RangActuel?> rang) =>
        joueurs
            .Select(j => (Joueur: j, Rang: rang(j)))
            .OrderByDescending(l => l.Rang) // non classé (null) en dernier
            .ThenBy(l => l.Joueur.Prenom, StringComparer.OrdinalIgnoreCase)
            .Select(l => new LigneClassementSoloQ(
                l.Joueur.Id, l.Joueur.Prenom, l.Joueur.RiotId, l.Rang?.Tier, l.Rang?.Division, l.Rang?.Lp, l.Joueur.User?.AvatarUrl))
            .ToList();
}

/// <summary>La ligne d'un Joueur dans une file du classement SoloQ.</summary>
/// <param name="Tier">Tier en majuscules (ex. « GOLD »), null si non classé dans la file.</param>
/// <param name="Division">1 à 4 (1 = I), null à partir de Master ou si non classé.</param>
/// <param name="Lp">LP actuels, null si non classé.</param>
public record LigneClassementSoloQ(
    int PlayerId,
    string Prenom,
    string RiotId,
    string? Tier,
    int? Division,
    int? Lp,
    string? AvatarUrl = null);
