using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Picker;

/// <summary>Les deux capitaines d'une Draft : celui de l'Équipe A et celui de l'Équipe B.</summary>
public sealed record Capitaines(int CapitaineA, int CapitaineB);

/// <summary>
/// La Draft du Picker, sans base ni HTTP : l'aléa est injecté. Deux capitaines, pris parmi les dix,
/// choisissent les huit autres joueurs un par un, en alternance à partir du premier pick.
/// </summary>
public static class Draft
{
    public const int PicksCount = 2 * (Equipes.PlayersPerTeam - 1);

    /// <summary>Les deux plus gros costs parmi les dix ; à cost égal, l'ordre de la sélection départage.</summary>
    public static Capitaines CapitainesParPlusGrosCost(IReadOnlyList<PickerPlayer> joueurs)
    {
        EnsureDix(joueurs.Count);
        var deux = joueurs
            .Select((joueur, rang) => (joueur, rang))
            .OrderByDescending(j => j.joueur.Cost).ThenBy(j => j.rang)
            .Take(2)
            .Select(j => j.joueur.Id)
            .ToList();
        return new Capitaines(deux[0], deux[1]);
    }

    /// <summary>Deux joueurs distincts tirés uniformément parmi les dix.</summary>
    public static Capitaines CapitainesAuHasard(IReadOnlyList<int> joueurs, IRandomSource random)
    {
        EnsureDix(joueurs.Count);
        var a = random.Next(joueurs.Count);
        var b = random.Next(joueurs.Count - 1);
        if (b >= a) b++;
        return new Capitaines(joueurs[a], joueurs[b]);
    }

    /// <summary>L'Équipe qui choisit en premier, à pile ou face.</summary>
    public static string PileOuFace(IRandomSource random) => random.Next(2) == 0 ? Equipes.A : Equipes.B;

    /// <summary>L'Équipe qui choisit à chacun des huit picks, en alternance à partir du premier pick.</summary>
    public static IReadOnlyList<string> OrdreDesPicks(string premierPick)
    {
        var second = Equipes.Other(premierPick);
        return Enumerable.Range(0, PicksCount).Select(i => i % 2 == 0 ? premierPick : second).ToList();
    }

    /// <summary>L'Équipe qui doit choisir après <paramref name="picksFaits"/> picks ; null quand la Draft est complète.</summary>
    public static string? EquipeAuTrait(string premierPick, int picksFaits) =>
        picksFaits < PicksCount ? OrdreDesPicks(premierPick)[picksFaits] : null;

    private static void EnsureDix(int count)
    {
        if (count != 2 * Equipes.PlayersPerTeam)
            throw new ArgumentException("Le Picker forme les Équipes à partir de dix joueurs.");
    }
}
