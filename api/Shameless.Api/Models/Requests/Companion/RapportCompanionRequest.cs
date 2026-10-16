using System.Text.Json;
using Shameless.Application.Companion;

namespace Shameless.Api.Models.Requests.Companion;

/// <summary>
/// Le rapport de fin de game du Companion : l'identifiant Riot stable de la partie, ses données Riot
/// brutes (objet JSON) et les dix joueurs détectés. La Série vient du code Companion, pas du rapport.
/// </summary>
public sealed record RapportCompanionRequest(string RiotMatchId, JsonElement Donnees, List<JoueurDuRapportRequest> Joueurs)
{
    public RapportDeGame ToRapport() => new(
        RiotMatchId ?? "",
        ObjetJson(Donnees),
        (Joueurs ?? []).Select(j => new JoueurDuRapport(
            j.Puuid ?? "", j.Side ?? "", j.ChampionId ?? "", j.Victoire, ObjetJson(j.Stats))).ToList());

    private static string? ObjetJson(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object ? element.GetRawText() : null;
}

/// <summary>
/// Un joueur détecté : son PUUID, son Side ("blue" | "red"), le champion joué (id Data Dragon, ex.
/// "LeeSin"), sa victoire et ses stats brutes (objet JSON).
/// </summary>
public sealed record JoueurDuRapportRequest(string Puuid, string Side, string ChampionId, bool Victoire, JsonElement Stats);
