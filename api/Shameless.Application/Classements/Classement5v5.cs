using Shameless.Application.Entities;

namespace Shameless.Application.Classements;

/// <summary>Le classement des customs 5v5 de l'Instance, distinct du classement SoloQ.</summary>
public record Classement5v5(List<LigneClassement5v5> Joueurs)
{
    /// <summary>
    /// Classe les Joueurs par winrate sur leurs games terminées (y compris celles d'une Série interrompue),
    /// puis par games jouées, puis par prénom. Un Joueur sans game terminée garde sa ligne, en fin de classement.
    /// </summary>
    /// <param name="participations">Les participations aux games, avec leur game : seules les terminées comptent.</param>
    public static Classement5v5 Calculer(IEnumerable<Player> joueurs, IEnumerable<CustomGameParticipant> participations)
    {
        var parJoueur = participations
            .Where(p => p.CustomGame.Status == GameStatuses.Completed)
            .GroupBy(p => p.PlayerId)
            .ToDictionary(g => g.Key, g => (Games: g.Count(), Victoires: g.Count(p => p.HasWon)));

        var lignes = joueurs
            .Select(j =>
            {
                var (games, victoires) = parJoueur.GetValueOrDefault(j.Id);
                return (Joueur: j, Games: games, Victoires: victoires,
                    Ratio: games == 0 ? (double?)null : (double)victoires / games);
            })
            .OrderByDescending(l => l.Ratio.HasValue)
            .ThenByDescending(l => l.Ratio)
            .ThenByDescending(l => l.Games)
            .ThenBy(l => l.Joueur.Prenom, StringComparer.OrdinalIgnoreCase)
            .Select(l => new LigneClassement5v5(
                l.Joueur.Id, l.Joueur.Prenom, l.Joueur.RiotId, l.Games, l.Victoires, l.Games - l.Victoires,
                l.Ratio is { } r ? Math.Round(r * 100, 1) : null, l.Joueur.User?.AvatarUrl))
            .ToList();

        return new Classement5v5(lignes);
    }
}

/// <summary>La ligne d'un Joueur au classement 5v5.</summary>
/// <param name="Winrate">Pourcentage de victoires (0 à 100, une décimale), null sans game terminée.</param>
public record LigneClassement5v5(
    int PlayerId,
    string Prenom,
    string RiotId,
    int GamesJouees,
    int Victoires,
    int Defaites,
    double? Winrate,
    string? AvatarUrl = null);
