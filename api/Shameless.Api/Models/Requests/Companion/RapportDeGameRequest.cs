using System.Text.Json;
using Shameless.Application.Companion;

namespace Shameless.Api.Models.Requests.Companion;

/// <summary>
/// Le rapport de fin de game du Companion : l'identifiant Riot stable de la partie, ses données Riot
/// brutes (objet JSON) et les dix joueurs détectés. La Série vient du code Companion, pas du rapport.
/// </summary>
/// <remarks>
/// <c>Chronologie</c> est facultative (ADR 0022) : sans elle le rapport reste valide. Elle donne des points
/// datés (<c>t</c> en secondes depuis le début) avec, par joueur, kills, deaths, assists, cs, level, vision,
/// itemsValue et dead, et des événements d'objectifs datés.
/// </remarks>
public sealed record RapportDeGameRequest(
    string RiotMatchId, JsonElement Donnees, List<JoueurDuRapportRequest> Joueurs, ChronologieRequest? Chronologie = null)
{
    public RapportDeGame ToRapport() => new(
        RiotMatchId ?? "",
        ObjetJson(Donnees),
        (Joueurs ?? []).Select(j => new JoueurDuRapport(
            j.Puuid ?? "", j.Side ?? "", j.ChampionId > 0 ? j.ChampionId.ToString(System.Globalization.CultureInfo.InvariantCulture) : "",
            j.Victoire, ObjetJson(j.Stats))).ToList(),
        Chronologie?.ToChronologie());

    private static string? ObjetJson(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object ? element.GetRawText() : null;
}

/// <summary>
/// Un joueur détecté : son PUUID, son Side ("blue" | "red"), le champion joué (l'id numérique du
/// client LoL, ex. 64 pour Lee Sin, que l'API convertit avec son catalogue Data Dragon), sa victoire et
/// ses stats brutes (objet JSON). Un id absent ou nul est refusé (400) ; un id que le catalogue ne
/// connaît pas laisse le rapport en attente (503), à renvoyer plus tard.
/// </summary>
public sealed record JoueurDuRapportRequest(string Puuid, string Side, int ChampionId, bool Victoire, JsonElement Stats);

/// <summary>La chronologie facultative du rapport (ADR 0022) : points datés par joueur et événements d'objectifs.</summary>
public sealed record ChronologieRequest(List<PointDeChronologieRequest?>? Points, List<EvenementDeChronologieRequest?>? Evenements)
{
    public ChronologieDeGame ToChronologie() => new(
        Points?.Select(p => p == null ? null! : new PointDeChronologie(
            p.T, p.Joueurs?.Select(j => j == null ? null! : new JoueurDuPoint(
                j.Puuid, j.Kills, j.Deaths, j.Assists, j.Cs, j.Level, j.Vision, j.ItemsValue, j.Dead)).ToList())).ToList(),
        Evenements?.Select(e => e == null ? null! : new EvenementDeChronologie(e.T, e.Type, e.Side)).ToList());
}

/// <param name="T">Les secondes écoulées depuis le début de la partie ; les points sont classés par date croissante.</param>
public sealed record PointDeChronologieRequest(int? T, List<JoueurDuPointRequest?>? Joueurs);

/// <summary>L'état d'un joueur (par PUUID) à un instant. Tous les champs sont obligatoires, aucun n'est deviné.</summary>
/// <param name="Level">Le niveau, de 1 à 18.</param>
/// <param name="Vision">Le score de vision.</param>
/// <param name="ItemsValue">La valeur totale des objets du joueur, en or.</param>
/// <param name="Dead">Vrai si le joueur est mort à cet instant.</param>
public sealed record JoueurDuPointRequest(
    string? Puuid, int? Kills, int? Deaths, int? Assists, int? Cs, int? Level, int? Vision, int? ItemsValue, bool? Dead);

/// <param name="Type">dragon, herald, baron, turret ou inhibitor.</param>
/// <param name="Side">Le side (« blue » ou « red ») de l'équipe qui a pris l'objectif.</param>
public sealed record EvenementDeChronologieRequest(int? T, string? Type, string? Side);
