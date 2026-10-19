using Shameless.Application.Entities;

namespace Shameless.Application.Votes;

/// <summary>
/// Le Hall de la Honte : le cumul historique des titres de DOFD, sans remise à zéro mensuelle.
/// </summary>
public sealed record HallDeLaHonte(List<LigneHallDeLaHonte> Joueurs)
{
    /// <summary>
    /// Classe les Joueurs ayant au moins un titre de DOFD, du plus au moins titré (puis par prénom). Les
    /// ex æquo partagent le même rang et le suivant est sauté (1, 1, 3).
    /// </summary>
    /// <param name="titres">Les titres de chaque Joueur, d'après les votes clos de toutes les Games terminées.</param>
    public static HallDeLaHonte Classer(IEnumerable<Player> joueurs, IReadOnlyDictionary<int, TitresDuJoueur> titres)
    {
        var classes = joueurs
            .Select(j => (Joueur: j, Dofd: titres.GetValueOrDefault(j.Id, TitresDuJoueur.Aucun).Dofd))
            .Where(l => l.Dofd > 0)
            .OrderByDescending(l => l.Dofd)
            .ThenBy(l => l.Joueur.Prenom, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lignes = new List<LigneHallDeLaHonte>(classes.Count);
        for (var i = 0; i < classes.Count; i++)
        {
            var rang = i > 0 && classes[i].Dofd == classes[i - 1].Dofd ? lignes[i - 1].Rang : i + 1;
            var (joueur, dofd) = classes[i];
            lignes.Add(new LigneHallDeLaHonte(rang, joueur.Id, joueur.Prenom, joueur.RiotId, dofd, joueur.User?.AvatarUrl));
        }
        return new HallDeLaHonte(lignes);
    }
}

/// <summary>La ligne d'un Joueur au Hall de la Honte.</summary>
/// <param name="Rang">Partagé par les ex æquo (1, 1, 3).</param>
/// <param name="Dofd">Nombre de titres de DOFD remportés, égalités comprises.</param>
/// <param name="AvatarUrl">Photo Discord du compte lié au Joueur ; null s'il n'est pas lié.</param>
public sealed record LigneHallDeLaHonte(int Rang, int PlayerId, string Prenom, string RiotId, int Dofd, string? AvatarUrl = null);
