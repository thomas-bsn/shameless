namespace Shameless.Application.Entities;

/// <summary>Les deux Équipes d'une Série, qui gardent leur identité quel que soit le Side.</summary>
public static class Equipes
{
    public const string A = "A";
    public const string B = "B";

    /// <summary>Une Équipe compte cinq joueurs.</summary>
    public const int PlayersPerTeam = 5;

    public static bool IsValid(string? equipe) => equipe is A or B;

    public static string Other(string equipe) => equipe == A ? B : A;
}

/// <summary>Le camp sur la carte pour une game donnée.</summary>
public static class Sides
{
    public const string Blue = "blue";
    public const string Red = "red";

    public static string Other(string side) => side == Blue ? Red : Blue;

    /// <summary>
    /// Le Side de l'Équipe A pour la prochaine game d'une Série : tiré à pile ou face pour la première,
    /// puis l'inverse de celui de la game précédente (le Side corrigé par un Rapport compris).
    /// </summary>
    public static string ProchainDeLEquipeA(string? sideDeLaGamePrecedente, Interfaces.IRandomSource random) =>
        sideDeLaGamePrecedente == null
            ? random.Next(2) == 0 ? Blue : Red
            : Other(sideDeLaGamePrecedente);
}

/// <summary>Le statut d'une game : en attente de son résultat, puis terminée.</summary>
public static class GameStatuses
{
    public const string Pending = "pending";
    public const string Completed = "completed";
}
