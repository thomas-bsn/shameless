using System.Globalization;
using System.Text.Json.Serialization;
using Shameless.Application.Entities;
using Shameless.Application.Picker;
using Shameless.Application.Stats;

namespace Shameless.Application.Bot;

/// <summary>
/// Les snapshots (<c>data</c>) des webhooks, au contrat du bot (<c>docs/shameless.md</c> de
/// harceleur_bot). Sérialisés en snake_case, champs null omis, sauf <c>discord_id</c> (null = Joueur
/// sans compte Discord lié). Les IDs sont en texte, stables d'un événement à l'autre.
/// </summary>
public static class SnapshotsDuBot
{
    /// <summary>
    /// <c>teams_formed</c> : les deux Équipes de la Série, dans leur composition courante. Équipe A →
    /// vocal Team 1, Équipe B → vocal Team 2, pour toute la Série.
    /// </summary>
    public static TeamsFormedData TeamsFormed(BestOfSeries series) => new(
        SeriesId: Id(series.Id),
        TeamA: EquipeDeLaSerie(series, Equipes.A),
        TeamB: EquipeDeLaSerie(series, Equipes.B));

    /// <summary>
    /// <c>captain_link</c> : le Lien capitaine de l'Équipe et son Capitaine, à qui le bot l'envoie en DM.
    /// Null tant que l'Équipe n'a pas de Capitaine : le lien attend sa désignation.
    /// </summary>
    public static CaptainLinkData? CaptainLink(BestOfSeries series, string equipe, string? webUrl)
    {
        var capitaine = series.PlayersOf(equipe).SingleOrDefault(p => p.Capitaine);
        if (capitaine == null) return null;
        return new CaptainLinkData(
            SeriesId: Id(series.Id),
            Team: equipe,
            TeamName: NomDe(equipe),
            Captain: new JoueurDuSnapshot(capitaine.Player.Prenom, GameDuBot.DiscordIdDe(capitaine.Player)),
            Url: Lien(webUrl, $"draft/capitaine/{Uri.EscapeDataString(series.LienCapitaine(equipe).Secret)}"));
    }

    /// <summary><c>spectator_link</c> : le Lien spectateur, que le bot poste dans le salon général.</summary>
    public static SpectatorLinkData SpectatorLink(BestOfSeries series, string? webUrl) => new(
        SeriesId: Id(series.Id),
        Url: Lien(webUrl, $"draft/spectateur/{Uri.EscapeDataString(series.LienSpectateur.Secret)}"));

    /// <summary><c>game_started</c> : la Game lancée, ses deux Équipes avec leur Side et ses participants liés.</summary>
    public static GameData GameStarted(CustomGame game, string? webUrl) => new(
        SeriesId: game.SeriesId is { } s ? Id(s) : null,
        GameId: Id(game.Id),
        GameNumber: game.NumeroDansLaSerie,
        BestOf: game.Series?.Format ?? 1,
        SeriesUrl: Lien(webUrl, game.SeriesId is { } id ? $"series/{id}" : null),
        TeamA: EquipeDeLaGame(game, Equipes.A, avecStats: false),
        TeamB: EquipeDeLaGame(game, Equipes.B, avecStats: false),
        ParticipantDiscordIds: ParticipantsLies(game));

    /// <summary>
    /// <c>game_completed</c> : comme <c>game_started</c>, avec le vainqueur, le score de la Série à
    /// l'issue de cette Game, sa durée, le lien de récap et les stats du Companion de chaque Joueur.
    /// </summary>
    public static GameData GameCompleted(CustomGame game, string? webUrl)
    {
        var jouees = game.Series?.Games.Where(g => g.Id <= game.Id && g.Status == GameStatuses.Completed).ToList()
                     ?? [game];
        return GameStarted(game, webUrl) with
        {
            TeamA = EquipeDeLaGame(game, Equipes.A, avecStats: true),
            TeamB = EquipeDeLaGame(game, Equipes.B, avecStats: true),
            Winner = game.WinningTeam,
            SeriesScore = new Dictionary<string, int>
            {
                [Equipes.A] = jouees.Count(g => g.WinningTeam == Equipes.A),
                [Equipes.B] = jouees.Count(g => g.WinningTeam == Equipes.B)
            },
            DurationSeconds = DonneesDuMatch.Lire(game.RiotMatchData)?.DurationSeconds,
            RecapUrl = Lien(webUrl, $"games/{game.Id}")
        };
    }

    private static EquipeDuSnapshot EquipeDeLaSerie(BestOfSeries series, string equipe) => new(
        Name: NomDe(equipe),
        Side: null,
        Players: series.PlayersOf(equipe).OrderBy(p => p.Id)
            .Select(p => new JoueurDuSnapshot(p.Player.Prenom, GameDuBot.DiscordIdDe(p.Player)))
            .ToList());

    private static EquipeDuSnapshot EquipeDeLaGame(CustomGame game, string equipe, bool avecStats)
    {
        var participants = game.ParticipantsOf(equipe).OrderBy(p => p.Id).ToList();
        var notes = avecStats ? NotesDeLaGame.Calculer(game) : null;
        var duree = DonneesDuMatch.Lire(game.RiotMatchData)?.DurationSeconds;
        var stats = participants.ToDictionary(p => p.Id, p => StatsDuParticipant.Lire(p.RiotParticipantData));
        var killsDeLEquipe = stats.Values.Any(s => s?.Kills is not null) ? stats.Values.Sum(s => s?.Kills ?? 0) : (int?)null;

        return new EquipeDuSnapshot(
            Name: NomDe(equipe),
            Side: equipe == Equipes.A ? game.TeamASide : game.TeamBSide,
            Players: participants.Select(p =>
            {
                var joueur = new JoueurDuSnapshot(p.Player.Prenom, GameDuBot.DiscordIdDe(p.Player));
                return avecStats ? AvecStats(joueur, p, stats[p.Id], notes![p.PlayerId], killsDeLEquipe, duree) : joueur;
            }).ToList());
    }

    private static JoueurDuSnapshot AvecStats(
        JoueurDuSnapshot joueur, CustomGameParticipant participant, StatsDuParticipant? stats, NoteDePerformance? note,
        int? killsDeLEquipe, int? duree)
    {
        var cs = stats is null ? null : FormulesDeStats.Cs(stats);
        return joueur with
        {
            Champion = participant.ChampionId,
            Role = note?.Role is { } role ? RoleDuContrat(role) : null,
            Kills = stats?.Kills,
            Deaths = stats?.Deaths,
            Assists = stats?.Assists,
            Cs = cs,
            CsPerMin = cs is { } c && duree is > 0 ? Math.Round(c / (duree.Value / 60.0), 1) : null,
            KillParticipation = stats?.Kills is { } k && killsDeLEquipe is > 0
                ? Math.Round(FormulesDeStats.ParticipationAuxKills(k, stats.Assists ?? 0, killsDeLEquipe.Value), 2)
                : null,
            Damage = stats?.DamageToChampions,
            PerformanceScore = note?.Note
        };
    }

    private static string RoleDuContrat(Role role) => role switch
    {
        Role.Top => "TOP",
        Role.Jungle => "JUNGLE",
        Role.Mid => "MID",
        Role.Adc => "ADC",
        _ => "SUPPORT"
    };

    private static List<string> ParticipantsLies(CustomGame game) =>
        game.Participants.OrderBy(p => p.Id)
            .Select(p => GameDuBot.DiscordIdDe(p.Player))
            .OfType<string>()
            .ToList();

    /// <summary>Shameless n'a pas de nom d'Équipe : « Équipe A » et « Équipe B ».</summary>
    private static string NomDe(string equipe) => $"Équipe {equipe}";

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);

    private static string? Lien(string? webUrl, string? chemin) =>
        string.IsNullOrWhiteSpace(webUrl) || chemin == null ? null : $"{webUrl.TrimEnd('/')}/{chemin}";
}

/// <summary>Le snapshot de <c>teams_formed</c>.</summary>
public sealed record TeamsFormedData(string SeriesId, EquipeDuSnapshot TeamA, EquipeDuSnapshot TeamB);

/// <summary>Le snapshot de <c>captain_link</c> : <c>url</c> est null si l'URL publique du site n'est pas configurée.</summary>
public sealed record CaptainLinkData(string SeriesId, string Team, string TeamName, JoueurDuSnapshot Captain, string? Url);

/// <summary>Le snapshot de <c>spectator_link</c> : <c>url</c> est null si l'URL publique du site n'est pas configurée.</summary>
public sealed record SpectatorLinkData(string SeriesId, string? Url);

/// <summary>Le snapshot de <c>game_started</c> et, complété, de <c>game_completed</c>.</summary>
public sealed record GameData(
    string? SeriesId,
    string GameId,
    int GameNumber,
    int BestOf,
    string? SeriesUrl,
    EquipeDuSnapshot TeamA,
    EquipeDuSnapshot TeamB,
    List<string> ParticipantDiscordIds,
    string? Winner = null,
    Dictionary<string, int>? SeriesScore = null,
    int? DurationSeconds = null,
    string? RecapUrl = null);

/// <summary>Une Équipe d'un snapshot ; <c>side</c> (« blue » | « red ») pour les événements de Game.</summary>
public sealed record EquipeDuSnapshot(string Name, string? Side, List<JoueurDuSnapshot> Players);

/// <summary>Un Joueur d'un snapshot ; ses stats (contrat #52) seulement dans <c>game_completed</c>, quand le Companion les a envoyées.</summary>
public sealed record JoueurDuSnapshot(
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? DiscordId,
    string? Champion = null,
    string? Role = null,
    int? Kills = null,
    int? Deaths = null,
    int? Assists = null,
    int? Cs = null,
    double? CsPerMin = null,
    double? KillParticipation = null,
    int? Damage = null,
    int? PerformanceScore = null);
