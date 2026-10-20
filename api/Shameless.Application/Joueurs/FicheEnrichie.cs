using Shameless.Application.Picker;

namespace Shameless.Application.Joueurs;

/// <summary>Comment la liste des games de la fiche se regroupe.</summary>
public enum Regroupement
{
    /// <summary>Un seul groupe.</summary>
    Plat,

    /// <summary>Un groupe par Soirée (les games hors Soirée dans un groupe à part).</summary>
    Soiree,

    /// <summary>Un groupe par jour calendaire dans le fuseau de l'Instance (comme le jour d'un clip, ADR 0014).</summary>
    Jour
}

/// <summary>Le résultat demandé par le filtre de la liste.</summary>
public enum ResultatFiltre
{
    Victoire,
    Defaite
}

/// <summary>Le regroupement et les filtres de la liste des games de la fiche ; un filtre null ne filtre rien.</summary>
public sealed record FiltreDeGames(
    Regroupement Regroupement = Regroupement.Plat,
    Role? Role = null,
    int? SerieId = null,
    string? Champion = null,
    ResultatFiltre? Resultat = null)
{
    internal bool Accepte(PartieDuJoueur p) =>
        (Role is null || p.Role == Role)
        && (SerieId is null || p.Game.SeriesId == SerieId)
        && (Champion is null || string.Equals(Stats.ValeursEffectives.Champion(p.Participant), Champion, StringComparison.OrdinalIgnoreCase))
        && (Resultat is null || p.Victoire == (Resultat == ResultatFiltre.Victoire));
}

/// <summary>Un coéquipier du Joueur : les games jouées dans la même Équipe.</summary>
/// <param name="Winrate">Pourcentage de victoires ensemble (une décimale).</param>
public sealed record CoequipierDeLaFiche(int PlayerId, string Prenom, int Parties, int Victoires, int Defaites, double Winrate);

/// <summary>Un champion parmi les plus joués des dernières games.</summary>
/// <param name="Winrate">Pourcentage de victoires (une décimale).</param>
/// <param name="Kda">Le KDA sur les games où le Companion a donné K/D/A, null sinon.</param>
public sealed record ChampionDuResume(string ChampionId, int Games, double Winrate, double? Kda);

/// <summary>
/// Le résumé des 30 dernières games terminées du Joueur. Les badges sont ceux de la game
/// (Meilleure perf, ACE), les titres sont ceux des votes clos : rien à voir avec le MVP voté pour les badges.
/// </summary>
public sealed record ResumeDesDernieresGames(
    int Games,
    int Victoires,
    int Defaites,
    double? Winrate,
    List<ChampionDuResume> TopChampions,
    double? Kda,
    double? NoteMoyenne,
    int MeilleuresPerfs,
    int Aces,
    int Mvp,
    int Dofd)
{
    /// <summary>Le nombre de games du résumé.</summary>
    public const int NombreDeGames = 30;

    internal static ResumeDesDernieresGames Calculer(IReadOnlyList<PartieDuJoueur> partiesRecentesDabord, IReadOnlyList<GameDeLaFiche> lignes)
    {
        var parties = partiesRecentesDabord.Take(NombreDeGames).ToList();
        var recentes = lignes.Take(NombreDeGames).ToList();
        var stats = StatsDeCustoms.Calculer(parties);
        return new ResumeDesDernieresGames(
            stats.GamesJouees,
            stats.Victoires,
            stats.Defaites,
            stats.Winrate,
            parties
                .Where(p => Stats.ValeursEffectives.Champion(p.Participant) is not null)
                .GroupBy(p => Stats.ValeursEffectives.Champion(p.Participant)!)
                .Select(g => (g.Key, Stats: StatsDeCustoms.Calculer(g.ToList())))
                .OrderByDescending(c => c.Stats.GamesJouees)
                .ThenBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(c => new ChampionDuResume(c.Key, c.Stats.GamesJouees, c.Stats.Winrate ?? 0, c.Stats.Kda))
                .ToList(),
            stats.Kda,
            stats.NoteMoyenne,
            recentes.Count(l => l.Badge == Stats.BadgeDeGame.MeilleurePerf),
            recentes.Count(l => l.Badge == Stats.BadgeDeGame.Ace),
            recentes.Count(l => l.Mvp),
            recentes.Count(l => l.Dofd));
    }
}

/// <summary>Un groupe de games de la liste : une Soirée, un jour, ou tout à plat.</summary>
/// <param name="SoireeId">La Soirée du groupe (regroupement par soirée), null hors Soirée ou sans ce regroupement.</param>
/// <param name="Jour">Le jour « aaaa-mm-jj » du groupe (regroupement par jour), null sinon.</param>
/// <param name="Games">Les games du groupe, de la plus récente à la plus ancienne.</param>
public sealed record GroupeDeGames(int? SoireeId, string? Jour, List<GameDeLaFiche> Games);

internal static class FicheEnrichie
{
    public static List<CoequipierDeLaFiche> JoueAvec(int playerId, IEnumerable<PartieDuJoueur> parties) =>
        parties
            .SelectMany(p => p.Game.Participants
                .Where(c => c.Side == p.Participant.Side && c.PlayerId != playerId)
                .Select(c => (c.Player, p.Victoire)))
            .GroupBy(c => c.Player.Id)
            .Select(g =>
            {
                var victoires = g.Count(c => c.Victoire);
                return new CoequipierDeLaFiche(g.Key, g.First().Player.Prenom, g.Count(), victoires, g.Count() - victoires,
                    Math.Round(100.0 * victoires / g.Count(), 1));
            })
            .OrderByDescending(c => c.Parties)
            .ThenBy(c => c.Prenom, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.PlayerId)
            .ToList();

    /// <summary>Les parties (déjà triées, récentes d'abord) filtrées puis groupées.</summary>
    public static List<GroupeDeGames> Liste(IEnumerable<(PartieDuJoueur Partie, GameDeLaFiche Ligne)> lignes, FiltreDeGames filtre, TimeZoneInfo fuseau)
    {
        var retenues = lignes.Where(l => filtre.Accepte(l.Partie)).ToList();
        if (retenues.Count == 0) return [];
        return filtre.Regroupement switch
        {
            Regroupement.Soiree => retenues
                .GroupBy(l => l.Partie.Game.Series?.SoireeId)
                .Select(g => new GroupeDeGames(g.Key, null, g.Select(l => l.Ligne).ToList()))
                .ToList(),
            Regroupement.Jour => retenues
                .GroupBy(l => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(l.Ligne.PlayedAt, DateTimeKind.Utc), fuseau)))
                .Select(g => new GroupeDeGames(null, g.Key.ToString("yyyy-MM-dd"), g.Select(l => l.Ligne).ToList()))
                .ToList(),
            _ => [new GroupeDeGames(null, null, retenues.Select(l => l.Ligne).ToList())]
        };
    }
}
