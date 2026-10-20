using System.Text.Json;
using System.Text.Json.Nodes;
using Shameless.Application.Companion;

namespace Shameless.Application.Entities;

/// <summary>Où en est un rapport du Companion gardé par Shameless.</summary>
public enum StatutRapport
{
    /// <summary>Le rapport est rattaché à sa game, qu'il a terminée.</summary>
    Accepte,

    /// <summary>Ses joueurs ne sont pas la composition de la game : l'organisateur décide.</summary>
    EnRevue,

    /// <summary>L'organisateur l'a écarté : la game attend un autre rapport ou une saisie manuelle.</summary>
    Rejete
}

/// <summary>
/// Un rapport du Companion mis en revue parce que ses joueurs détectés ne sont pas la composition de
/// la game en attente. Il garde le rapport tel que reçu jusqu'à la décision de l'organisateur, puis
/// reste comme trace. Il disparaît avec sa game (Série interrompue ou annulée, ADR 0009).
/// </summary>
public class RapportCompanion
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public CustomGame Game { get; set; } = null!;
    public string RiotMatchId { get; set; } = string.Empty;

    /// <summary>Le rapport reçu (JSON) : données Riot de la partie et dix joueurs détectés.</summary>
    public string Contenu { get; set; } = "{}";

    /// <summary>Jeton de concurrence : un rapport en revue n'est accepté ou rejeté qu'une fois.</summary>
    public StatutRapport Statut { get; set; } = StatutRapport.EnRevue;
    public DateTime RecuLe { get; set; }
    public DateTime? DecideLe { get; set; }

    public static RapportCompanion EnRevue(RapportDeGame rapport, DateTime recuLe) => new()
    {
        RiotMatchId = rapport.RiotMatchId,
        Contenu = Serialiser(rapport),
        Statut = StatutRapport.EnRevue,
        RecuLe = recuLe
    };

    // Le rapport lu, et le contenu dont il vient : relu seulement si le contenu change.
    private (string Contenu, RapportDeGame Rapport)? _lu;

    /// <summary>Le rapport tel que reçu.</summary>
    public RapportDeGame Rapport()
    {
        if (_lu is not { } lu || !ReferenceEquals(lu.Contenu, Contenu))
            _lu = lu = (Contenu, Lire());
        return lu.Rapport;
    }

    private RapportDeGame Lire()
    {
        var contenu = JsonNode.Parse(Contenu)!;
        return new RapportDeGame(
            RiotMatchId,
            contenu["donnees"]!.ToJsonString(),
            contenu["joueurs"]!.AsArray().Select(j => new JoueurDuRapport(
                j!["puuid"]!.GetValue<string>(),
                j["side"]!.GetValue<string>(),
                j["championId"]!.GetValue<string>(),
                j["victoire"]!.GetValue<bool>(),
                j["stats"]!.ToJsonString(),
                j["riotId"]?.GetValue<string>())).ToList(),
            ChronologieDeGame.Lire(contenu["chronologie"]?.ToJsonString()));
    }

    /// <summary>L'organisateur tranche : le rapport en revue est accepté ou rejeté.</summary>
    public void Decider(StatutRapport decision, DateTime le)
    {
        Statut = decision;
        DecideLe = le;
    }

    private static string Serialiser(RapportDeGame rapport) => new JsonObject
    {
        ["donnees"] = JsonNode.Parse(rapport.Donnees!),
        ["joueurs"] = new JsonArray(rapport.Joueurs.Select(j => (JsonNode)new JsonObject
        {
            ["puuid"] = j.Puuid,
            ["side"] = j.Side,
            ["championId"] = j.ChampionId,
            ["victoire"] = j.Victoire,
            ["stats"] = JsonNode.Parse(j.Stats!),
            ["riotId"] = j.RiotId
        }).ToArray()),
        ["chronologie"] = rapport.Chronologie == null ? null : JsonNode.Parse(rapport.Chronologie.ToJson())
    }.ToJsonString(new JsonSerializerOptions());
}
