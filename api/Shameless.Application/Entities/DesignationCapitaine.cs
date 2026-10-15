namespace Shameless.Application.Entities;

/// <summary>
/// Comment l'organisateur désigne le Capitaine d'une Équipe dans une Série (après une Roulette, une
/// Revanche ou un Remplacement du capitaine) : à la main, au hasard, ou le plus gros cost de l'Équipe.
/// </summary>
public enum DesignationCapitaine
{
    Manuel,
    AuHasard,
    PlusGrosCost
}
