using System.Text.Json;
using Shameless.Application.Companion;

namespace Shameless.Api.Models.Requests.Companion;

/// <summary>
/// Le rapport de fin de game du Companion : l'identifiant Riot stable de la partie, ses données Riot
/// brutes (objet JSON) et les dix joueurs détectés. La Série vient du code Companion, pas du rapport.
/// </summary>
public sealed record RapportDeGameRequest(string RiotMatchId, JsonElement Donnees, List<JoueurDuRapportRequest> Joueurs)
{
    public RapportDeGame ToRapport() => new(
        RiotMatchId ?? "",
        ObjetJson(Donnees),
        (Joueurs ?? []).Select(j => new JoueurDuRapport(
            j.Puuid ?? "", j.Side ?? "", j.ChampionId > 0 ? j.ChampionId.ToString(System.Globalization.CultureInfo.InvariantCulture) : "",
            j.Victoire, ObjetJson(j.Stats))).ToList());

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
