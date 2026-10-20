namespace Shameless.Application.Entities;

/// <summary>
/// Le bulletin d'un votant pour une Game : au plus un Joueur pour le titre de DOFD. Le votant est identifié par son ID Discord (Joueur lié ou Spectateur présent en
/// vocal) : un Spectateur n'a en général pas d'Utilisateur, donc pas de clé vers <c>users</c>.
/// </summary>
public class BulletinDeVote
{
    public int GameId { get; set; }
    public CustomGame Game { get; set; } = null!;

    /// <summary>L'ID Discord (snowflake) du votant.</summary>
    public long VotantDiscordId { get; set; }

    /// <summary>Le Joueur choisi comme DOFD, null en cas d'abstention.</summary>
    public int? DofdPlayerId { get; set; }

    /// <summary>Le dernier dépôt du bulletin (UTC).</summary>
    public DateTime VoteLe { get; set; }
}
