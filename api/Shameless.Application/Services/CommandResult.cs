namespace Shameless.Application.Services;

/// <summary>
/// Le résultat d'une commande métier : réussie, ou refusée avec une raison en français.
/// L'API traduit <see cref="CommandStatus"/> en code HTTP (404, 400, 409, 429) et <c>Detail</c> en ProblemDetails.
/// </summary>
public enum CommandStatus
{
    Ok,
    NotFound,
    Invalid,
    Conflict,
    TooManyRequests
}

public sealed record CommandResult<T>(CommandStatus Status, T? Value, string? Detail)
{
    public bool IsOk => Status == CommandStatus.Ok;

    public static CommandResult<T> Ok(T value) => new(CommandStatus.Ok, value, null);
    public static CommandResult<T> NotFound(string detail) => new(CommandStatus.NotFound, default, detail);
    public static CommandResult<T> Invalid(string detail) => new(CommandStatus.Invalid, default, detail);
    public static CommandResult<T> Conflict(string detail) => new(CommandStatus.Conflict, default, detail);
    public static CommandResult<T> TooManyRequests(string detail) => new(CommandStatus.TooManyRequests, default, detail);

    /// <summary>Propage un refus vers une commande d'un autre type.</summary>
    public CommandResult<TOther> As<TOther>() => new(Status, default, Detail);
}
