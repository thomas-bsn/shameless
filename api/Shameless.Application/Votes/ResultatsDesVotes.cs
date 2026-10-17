using Shameless.Application.Entities;

namespace Shameless.Application.Votes;

/// <summary>
/// La fenêtre des votes d'une Game : ouverte à sa fin (<c>CompletedAt</c>), close 10 minutes plus tard.
/// </summary>
public static class FenetreDeVote
{
    public static readonly TimeSpan Duree = TimeSpan.FromMinutes(10);

    public const string AVenir = "a_venir";
    public const string Ouverts = "ouverts";
    public const string Clos = "clos";

    /// <summary>« a_venir » tant que la Game n'est pas terminée, « ouverts » pendant 10 minutes, puis « clos ».</summary>
    public static string Statut(CustomGame game, DateTime maintenant) =>
        game.Status != GameStatuses.Completed || game.CompletedAt is null ? AVenir
        : maintenant < game.CompletedAt.Value + Duree ? Ouverts
        : Clos;
}

/// <summary>
/// Les résultats des votes MVP et DOFD d'une Game.
/// </summary>
/// <param name="Statut">« a_venir » (Game en cours), « ouverts » ou « clos » (voir <see cref="FenetreDeVote"/>).</param>
/// <param name="OuvertureLe">La fin de la Game, null tant qu'elle n'est pas terminée.</param>
/// <param name="FermetureLe">10 minutes après la fin de la Game, null tant qu'elle n'est pas terminée.</param>
/// <param name="Votants">Le nombre de votants ayant choisi au moins un Joueur.</param>
public sealed record ResultatsDesVotes(
    int GameId,
    string Statut,
    DateTime? OuvertureLe,
    DateTime? FermetureLe,
    int Votants,
    ResultatDuTitre Mvp,
    ResultatDuTitre Dofd)
{
    /// <summary>
    /// Les résultats d'une Game chargée avec ses participants et leurs Joueurs, d'après ses bulletins.
    /// </summary>
    public static ResultatsDesVotes Calculer(CustomGame game, IEnumerable<BulletinDeVote> bulletins, DateTime maintenant)
    {
        var liste = bulletins.ToList();
        var statut = FenetreDeVote.Statut(game, maintenant);
        var clos = statut == FenetreDeVote.Clos;
        return new ResultatsDesVotes(
            game.Id,
            statut,
            game.CompletedAt,
            game.CompletedAt + FenetreDeVote.Duree,
            liste.Count(b => b.MvpPlayerId is not null || b.DofdPlayerId is not null),
            ResultatDuTitre.Calculer(game, liste.Select(b => b.MvpPlayerId), clos),
            ResultatDuTitre.Calculer(game, liste.Select(b => b.DofdPlayerId), clos));
    }
}

/// <summary>Le résultat d'un titre (MVP ou DOFD).</summary>
/// <param name="Gagnants">
/// Les Joueurs ayant reçu le plus de voix, tous reconnus en cas d'égalité ; vide tant que les votes
/// ne sont pas clos ou si personne n'a voté pour ce titre.
/// </param>
/// <param name="Decompte">
/// Les voix reçues par chaque Joueur qui en a, de la plus à la moins votée ; vide tant que les votes
/// ne sont pas clos, pour ne pas influencer les votants.
/// </param>
public sealed record ResultatDuTitre(List<int> Gagnants, List<VoixDuJoueur> Decompte)
{
    public static ResultatDuTitre Calculer(CustomGame game, IEnumerable<int?> choix, bool clos)
    {
        if (!clos) return new ResultatDuTitre([], []);
        var prenoms = game.Participants.ToDictionary(p => p.PlayerId, p => p.Player.Prenom);
        var decompte = choix
            .OfType<int>()
            .GroupBy(playerId => playerId)
            .Select(g => new VoixDuJoueur(g.Key, prenoms.GetValueOrDefault(g.Key, ""), g.Count()))
            .OrderByDescending(v => v.Voix)
            .ThenBy(v => v.Prenom, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var max = decompte.Count == 0 ? 0 : decompte[0].Voix;
        var gagnants = decompte.Where(v => v.Voix == max).Select(v => v.PlayerId).ToList();
        return new ResultatDuTitre(gagnants, decompte);
    }
}

/// <summary>Les voix reçues par un Joueur pour un titre.</summary>
public sealed record VoixDuJoueur(int PlayerId, string Prenom, int Voix);
