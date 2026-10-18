using Shameless.Application.Entities;
using Shameless.Application.Discord;

namespace Shameless.Application.Clips;

/// <summary>Ce que le bot dépose pour une vidéo de <c>#clips</c>, la vidéo à part (ADR 0014).</summary>
public sealed record DepotDeClip(
    long MessageId,
    long AttachmentId,
    long AuteurDiscordId,
    string AuteurNom,
    DateTime PublieLe,
    string NomFichier,
    string ContentType,
    long Taille);

/// <summary>
/// Le clip déposé, tel que le bot le lit (<c>POST /bot/clips</c>).
/// </summary>
/// <param name="GameId">La Game du clip, null s'il attend un rattachement manuel.</param>
/// <param name="Rattachement">« automatique » ou « manuel », null sans Game.</param>
public sealed record ClipDuBot(int ClipId, int? GameId, string? Rattachement)
{
    public static ClipDuBot From(Clip clip) => new(clip.Id, clip.GameId, clip.RattachementEffectif);
}

/// <summary>
/// Un clip tel que le site l'affiche. La vidéo se lit sur <c>GET /clips/{id}/fichier</c>.
/// </summary>
/// <param name="AuteurDiscordId">L'auteur du message Discord (snowflake, en texte).</param>
/// <param name="GameId">La Game du clip, null s'il attend un rattachement manuel.</param>
/// <param name="Rattachement">« automatique » ou « manuel », null sans Game.</param>
/// <param name="Taille">Taille de la vidéo, en octets.</param>
public sealed record ClipResponse(
    int Id,
    int? GameId,
    string? Rattachement,
    string AuteurNom,
    string AuteurDiscordId,
    DateTime PublieLe,
    string NomFichier,
    string ContentType,
    long Taille)
{
    public static ClipResponse From(Clip clip) => new(
        clip.Id,
        clip.GameId,
        clip.RattachementEffectif,
        clip.AuteurNom,
        DiscordId.Texte(clip.AuteurDiscordId),
        clip.PublieLe,
        clip.NomFichier,
        clip.ContentType,
        clip.Taille);
}

/// <summary>La Game d'un clip dans la liste (<c>GET /clips</c>), avec le score à son issue.</summary>
/// <param name="SeriesId">La Série de la game, null pour une game legacy hors Série.</param>
/// <param name="GameNumber">Le rang de la game dans sa Série, à partir de 1.</param>
/// <param name="BestOf">Le format de la Série (BO1, BO3…), null hors Série.</param>
/// <param name="LanceeLe">Quand la game a été lancée (UTC).</param>
/// <param name="ScoreA">Les victoires de l'Équipe A dans la Série, game comprise (0 ou 1 hors Série).</param>
/// <param name="ScoreB">Les victoires de l'Équipe B dans la Série, game comprise (0 ou 1 hors Série).</param>
public sealed record GameDeLaListeDeClips(
    int GameId, int? SeriesId, int GameNumber, int? BestOf, DateTime LanceeLe, int ScoreA, int ScoreB)
{
    public static GameDeLaListeDeClips From(CustomGame game)
    {
        var jouees = game.Series == null
            ? [game]
            : game.Series.Games.Where(g => g.Id <= game.Id).ToList();
        return new(
            game.Id, game.SeriesId, game.NumeroDansLaSerie, game.Series?.Format, game.CreatedAt,
            jouees.Count(g => g.WinningTeam == Equipes.A), jouees.Count(g => g.WinningTeam == Equipes.B));
    }
}

/// <summary>Un clip dans la liste de l'onglet Clips (<c>GET /clips</c>) : le clip et sa Game, si rattaché.</summary>
/// <param name="AuteurDiscordId">L'auteur du message Discord (snowflake, en texte).</param>
/// <param name="Rattachement">« automatique » ou « manuel », null sans Game.</param>
/// <param name="Taille">Taille de la vidéo, en octets.</param>
/// <param name="MasqueDuFond">true si un administrateur a retiré le clip du Fond.</param>
/// <param name="Game">La Game du clip, null s'il attend un rattachement manuel.</param>
public sealed record ClipDeLaListe(
    int Id,
    int? GameId,
    string? Rattachement,
    string AuteurNom,
    string AuteurDiscordId,
    DateTime PublieLe,
    string NomFichier,
    string ContentType,
    long Taille,
    bool MasqueDuFond,
    GameDeLaListeDeClips? Game)
{
    public static ClipDeLaListe From(Clip clip) => new(
        clip.Id,
        clip.GameId,
        clip.RattachementEffectif,
        clip.AuteurNom,
        DiscordId.Texte(clip.AuteurDiscordId),
        clip.PublieLe,
        clip.NomFichier,
        clip.ContentType,
        clip.Taille,
        clip.MasqueDuFond,
        clip.Game is null ? null : GameDeLaListeDeClips.From(clip.Game));
}

/// <summary>Une page de la liste des clips, du plus récent au plus ancien publié.</summary>
/// <param name="Total">Le nombre de clips qui correspondent aux filtres, toutes pages confondues.</param>
/// <param name="Page">Le numéro de la page, à partir de 1.</param>
/// <param name="Taille">Le nombre de clips par page demandé.</param>
public sealed record ClipsPage(List<ClipDeLaListe> Items, int Total, int Page, int Taille);

/// <summary>Les filtres de la liste des clips ; chacun est facultatif. Bornes UTC [SoireeDebut, SoireeFin[.</summary>
/// <param name="Auteur">Un morceau du nom de l'auteur, sans tenir compte de la casse.</param>
/// <param name="SansGame">true : seulement sans Game ; false : seulement avec une Game.</param>
public sealed record FiltreDesClips(string? Auteur, DateTime? SoireeDebut, DateTime? SoireeFin, bool? SansGame);

/// <summary>
/// Un clip éligible au Fond (<c>GET /clips/fond</c>) : de quoi le lire et créditer son auteur. La vidéo se
/// lit sur <c>GET /clips/{id}/fichier</c>.
/// </summary>
/// <param name="GameId">La Game du clip, null s'il n'en a pas.</param>
public sealed record ClipDuFond(int Id, string AuteurNom, int? GameId)
{
    public static ClipDuFond From(Clip clip) => new(clip.Id, clip.AuteurNom, clip.GameId);
}

/// <summary>
/// Une Game proposée pour rattacher un clip à la main (<c>GET /clips/{id}/games-candidates</c>).
/// </summary>
/// <param name="SeriesId">La Série de la game, null pour une game legacy hors Série.</param>
/// <param name="GameNumber">Le rang de la game dans sa Série, à partir de 1.</param>
/// <param name="BestOf">Le format de la Série (BO1, BO3…), null hors Série.</param>
/// <param name="LanceeLe">Quand la game a été lancée (UTC).</param>
public sealed record GameCandidateDuClip(int GameId, int? SeriesId, int GameNumber, int? BestOf, DateTime LanceeLe)
{
    public static GameCandidateDuClip From(CustomGame game) =>
        new(game.Id, game.SeriesId, game.NumeroDansLaSerie, game.Series?.Format, game.CreatedAt);
}

/// <summary>La vidéo d'un clip, à servir : chemin absolu et type.</summary>
public sealed record FichierDeClip(string Chemin, string ContentType, string NomFichier);

/// <summary>L'état « masqué du Fond » d'un clip, rendu après l'avoir masqué ou réaffiché.</summary>
public sealed record MasqueDuFondDuClip(int Id, bool MasqueDuFond);
