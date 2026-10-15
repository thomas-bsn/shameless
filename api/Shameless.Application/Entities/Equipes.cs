namespace Shameless.Application.Entities;

/// <summary>Les deux Équipes d'une Série, qui gardent leur identité quel que soit le Side.</summary>
public static class Equipes
{
    public const string A = "A";
    public const string B = "B";

    public static bool IsValid(string? equipe) => equipe is A or B;

    public static string Other(string equipe) => equipe == A ? B : A;
}

/// <summary>Le camp sur la carte pour une game donnée.</summary>
public static class Sides
{
    public const string Blue = "blue";
    public const string Red = "red";

    public static string Other(string side) => side == Blue ? Red : Blue;
}

/// <summary>Le statut d'une game : en attente de son résultat, puis terminée.</summary>
public static class GameStatuses
{
    public const string Pending = "pending";
    public const string Completed = "completed";
}
