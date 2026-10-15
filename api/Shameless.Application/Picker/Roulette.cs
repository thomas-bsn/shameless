using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Picker;

/// <summary>Un des dix joueurs du Picker, avec son cost.</summary>
public sealed record PickerPlayer(int Id, decimal Cost);

/// <summary>Deux Équipes de cinq formées par le Picker.</summary>
public sealed record Composition(IReadOnlyList<int> TeamA, IReadOnlyList<int> TeamB);

/// <summary>
/// La Roulette du Picker, sans base ni HTTP : l'aléa est injecté.
/// <b>Aléatoire</b> : partition uniforme des dix joueurs en deux Équipes de cinq.
/// <b>Équilibrée</b> : tirage uniforme parmi les 126 partitions dont l'écart de cost total est
/// ≤ <see cref="EcartMaxEquilibre"/>, sinon parmi celles d'écart minimal.
/// </summary>
public static class Roulette
{
    public const decimal EcartMaxEquilibre = 1m;

    public static Composition Aleatoire(IReadOnlyList<int> joueurs, IRandomSource random)
    {
        EnsureDix(joueurs.Count);
        var melange = joueurs.ToArray();
        for (var i = melange.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (melange[i], melange[j]) = (melange[j], melange[i]);
        }
        return new Composition(melange[..Equipes.PlayersPerTeam], melange[Equipes.PlayersPerTeam..]);
    }

    public static Composition Equilibree(IReadOnlyList<PickerPlayer> joueurs, IRandomSource random)
    {
        var partitions = Partitions(joueurs.Select(j => j.Id).ToList())
            .Select(p => (Composition: p, Ecart: EcartCost(p, joueurs)))
            .ToList();

        var equilibrees = partitions.Where(p => p.Ecart <= EcartMaxEquilibre).ToList();
        var candidates = equilibrees.Count > 0
            ? equilibrees
            : partitions.Where(p => p.Ecart == partitions.Min(m => m.Ecart)).ToList();

        var tiree = candidates[random.Next(candidates.Count)].Composition;
        // Les 126 partitions mettent toutes le premier joueur en Équipe A : pile ou face pour les Équipes.
        return random.Next(2) == 0 ? tiree : new Composition(tiree.TeamB, tiree.TeamA);
    }

    /// <summary>La différence absolue entre les costs totaux des deux Équipes.</summary>
    public static decimal EcartCost(Composition composition, IReadOnlyCollection<PickerPlayer> joueurs)
    {
        var costs = joueurs.ToDictionary(j => j.Id, j => j.Cost);
        return Math.Abs(composition.TeamA.Sum(id => costs[id]) - composition.TeamB.Sum(id => costs[id]));
    }

    /// <summary>
    /// Les 126 partitions distinctes des dix joueurs en deux Équipes de cinq : le premier joueur est
    /// toujours en Équipe A, avec quatre des neuf autres.
    /// </summary>
    public static IEnumerable<Composition> Partitions(IReadOnlyList<int> joueurs)
    {
        EnsureDix(joueurs.Count);
        var autres = joueurs.Skip(1).ToList();
        for (var masque = 0; masque < 1 << autres.Count; masque++)
        {
            if (System.Numerics.BitOperations.PopCount((uint)masque) != Equipes.PlayersPerTeam - 1) continue;

            var teamA = new List<int> { joueurs[0] };
            var teamB = new List<int>();
            for (var i = 0; i < autres.Count; i++)
                ((masque & (1 << i)) != 0 ? teamA : teamB).Add(autres[i]);
            yield return new Composition(teamA, teamB);
        }
    }

    private static void EnsureDix(int count)
    {
        if (count != 2 * Equipes.PlayersPerTeam)
            throw new ArgumentException("Le Picker forme les Équipes à partir de dix joueurs.");
    }
}
