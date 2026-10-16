using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Shameless.Application.Picker;

namespace Shameless.Application.ChampionDraft;

/// <summary>
/// La désignation du Capitaine d'une Équipe dans une Série, sans base ni HTTP : l'aléa est injecté.
/// Parmi la composition courante de l'Équipe : le joueur choisi, un joueur au hasard, ou le plus gros
/// cost (à cost égal, l'ordre de la composition départage).
/// </summary>
public static class DesignationDesCapitaines
{
    /// <summary>
    /// Le joueur désigné capitaine parmi <paramref name="equipe"/>, dans l'ordre de sa composition ;
    /// null quand le joueur choisi (<see cref="DesignationCapitaine.Manuel"/>) n'est pas dans l'Équipe.
    /// </summary>
    public static int? Designer(
        DesignationCapitaine designation, IReadOnlyList<PickerPlayer> equipe, int? playerId, IRandomSource random) =>
        designation switch
        {
            DesignationCapitaine.Manuel => equipe.Any(j => j.Id == playerId) ? playerId : null,
            DesignationCapitaine.AuHasard => equipe[random.Next(equipe.Count)].Id,
            DesignationCapitaine.PlusGrosCost => equipe
                .Select((joueur, rang) => (joueur, rang))
                .OrderByDescending(j => j.joueur.Cost).ThenBy(j => j.rang)
                .First().joueur.Id,
            _ => throw new ArgumentOutOfRangeException(nameof(designation), designation, "Désignation du capitaine inconnue.")
        };
}
