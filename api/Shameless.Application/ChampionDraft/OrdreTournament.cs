using Shameless.Application.Entities;

namespace Shameless.Application.ChampionDraft;

/// <summary>Les deux actions d'une Draft de champions.</summary>
public static class ActionsDraft
{
    public const string Ban = "ban";
    public const string Pick = "pick";
}

/// <summary>Un tour de la Draft de champions : son numéro (1 à 20), l'action et le Side qui la joue.</summary>
public sealed record TourDraft(int Numero, string Action, string Side);

/// <summary>
/// L'ordre fixe du mode Tournament, sans base ni HTTP : vingt tours, bans puis picks, par Side.
/// L'Équipe attendue à un tour dépend de l'Équipe côté blue pour la game.
/// </summary>
public static class OrdreTournament
{
    public const int ToursCount = 20;

    private const string B = Sides.Blue;
    private const string R = Sides.Red;

    // bans B1 R1 B2 R2 B3 R3, picks B1 R1 R2 B2 B3 R3, bans R4 B4 R5 B5, picks R4 B4 B5 R5
    private static readonly (string Action, string Side)[] Ordre =
    [
        (ActionsDraft.Ban, B), (ActionsDraft.Ban, R), (ActionsDraft.Ban, B),
        (ActionsDraft.Ban, R), (ActionsDraft.Ban, B), (ActionsDraft.Ban, R),
        (ActionsDraft.Pick, B), (ActionsDraft.Pick, R), (ActionsDraft.Pick, R),
        (ActionsDraft.Pick, B), (ActionsDraft.Pick, B), (ActionsDraft.Pick, R),
        (ActionsDraft.Ban, R), (ActionsDraft.Ban, B), (ActionsDraft.Ban, R), (ActionsDraft.Ban, B),
        (ActionsDraft.Pick, R), (ActionsDraft.Pick, B), (ActionsDraft.Pick, B), (ActionsDraft.Pick, R)
    ];

    public static IReadOnlyList<TourDraft> Tours { get; } =
        Ordre.Select((tour, i) => new TourDraft(i + 1, tour.Action, tour.Side)).ToList();

    /// <summary>Le tour numéro <paramref name="numero"/>, de 1 à 20.</summary>
    public static TourDraft Tour(int numero)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(numero, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(numero, ToursCount);
        return Tours[numero - 1];
    }

    /// <summary>Le tour à jouer après <paramref name="toursJoues"/> tours ; null quand la Draft de champions est terminée.</summary>
    public static TourDraft? TourCourant(int toursJoues) =>
        toursJoues < ToursCount ? Tour(toursJoues + 1) : null;

    /// <summary>L'Équipe qui joue le tour <paramref name="numero"/>, selon l'Équipe côté blue.</summary>
    public static string EquipeAttendue(int numero, string equipeBlue) =>
        Tour(numero).Side == Sides.Blue ? equipeBlue : Equipes.Other(equipeBlue);
}
