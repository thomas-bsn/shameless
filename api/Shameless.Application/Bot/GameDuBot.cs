using Shameless.Application.Entities;
using Shameless.Application.Discord;

namespace Shameless.Application.Bot;

/// <summary>
/// Une Game telle que le bot Discord la lit (<c>GET /bot/games/{id}</c>) : son état et ses dix joueurs
/// avec leur compte Discord lié, de quoi proposer les votes et pronostics de la Phase 2.
/// </summary>
/// <param name="Status">« pending » tant que le résultat n'est pas saisi, puis « completed ».</param>
/// <param name="StartedAt">Le début de la Game (son lancement, ADR 0013).</param>
/// <param name="Winner">L'Équipe gagnante, « A » ou « B », null tant que la game n'est pas terminée.</param>
public sealed record GameDuBot(
    int GameId,
    int? SeriesId,
    int GameNumber,
    int BestOf,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? Winner,
    List<JoueurDuBot> TeamA,
    List<JoueurDuBot> TeamB)
{
    /// <summary>La game chargée avec ses participants (joueur et compte lié) et sa Série (games).</summary>
    public static GameDuBot From(CustomGame game)
    {
        List<JoueurDuBot> Equipe(string equipe) => JoueurDuBot.Equipe(game.ParticipantsOf(equipe).Select(p => (p.Id, p.Player)));
        return new GameDuBot(
            GameId: game.Id,
            SeriesId: game.SeriesId,
            GameNumber: game.NumeroDansLaSerie,
            BestOf: game.Series?.Format ?? 1,
            Status: game.Status,
            StartedAt: game.CreatedAt,
            CompletedAt: game.CompletedAt,
            Winner: game.WinningTeam,
            TeamA: Equipe(Equipes.A),
            TeamB: Equipe(Equipes.B));
    }

    /// <summary>L'ID Discord (snowflake, en texte : trop grand pour un nombre JSON) du compte lié, ou null.</summary>
    public static string? DiscordIdDe(Player player) => player.User is { } user ? DiscordId.Texte(user.DiscordId) : null;
}

/// <summary>Un joueur d'une Game, avec l'ID Discord de son compte lié (null sans lien).</summary>
public sealed record JoueurDuBot(int PlayerId, string Prenom, string? DiscordId)
{
    /// <summary>Une Équipe (joueurs chargés avec leur compte lié), dans l'ordre de sa composition.</summary>
    public static List<JoueurDuBot> Equipe(IEnumerable<(int Ordre, Player Player)> joueurs) => joueurs
        .OrderBy(j => j.Ordre)
        .Select(j => new JoueurDuBot(j.Player.Id, j.Player.Prenom, GameDuBot.DiscordIdDe(j.Player)))
        .ToList();
}
