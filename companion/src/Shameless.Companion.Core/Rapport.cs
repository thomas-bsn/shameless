using System.Text.Json;
using System.Text.Json.Nodes;

namespace Shameless.Companion.Core;

/// <summary>Un joueur du Rapport (JoueurDuRapportRequest). <c>Puuid</c> est null quand le client n'en donne pas : on ne l'invente pas.</summary>
public sealed record JoueurDuRapport(string? Puuid, string Side, int ChampionId, bool Victoire, JsonElement Stats);

/// <summary>Le Rapport d'une game (RapportDeGameRequest). Stats brutes de Riot : aucun calcul ni conversion (ADR 0019).</summary>
public sealed record RapportDeGame(string RiotMatchId, JsonObject Donnees, IReadOnlyList<JoueurDuRapport> Joueurs)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<JoueurDuRapport> JoueursSansPuuid => Joueurs.Where(j => j.Puuid is null).ToList();
}

public abstract record ResultatRapport
{
    private ResultatRapport() { }

    public sealed record Construit(RapportDeGame Rapport) : ResultatRapport;

    /// <summary>Pas un Rapport à envoyer : hors custom 5v5 sur la Faille, ou JSON inexploitable. La raison va au journal.</summary>
    public sealed record Ignore(string Raison) : ResultatRapport;
}

public static class RapportJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialiser(RapportDeGame rapport) => JsonSerializer.Serialize(rapport, Options);
}

/// <summary>
/// Construit le Rapport depuis le bloc de fin de game et la game de l'historique local (fonction pure).
/// Le Companion ne sert qu'aux customs 5v5 sur la Faille ; le reste est ignoré.
/// </summary>
public static class ConstructeurRapport
{
    private const int MapFaille = 11;

    /// <summary>Contrôles qui ne demandent que le bloc de fin de game : null si la partie peut devenir un Rapport.</summary>
    public static string? RaisonDIgnorer(string eogJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(eogJson);
            return RaisonDIgnorer(doc.RootElement);
        }
        catch (JsonException)
        {
            return "bloc de fin de game illisible";
        }
    }

    public static ResultatRapport Construire(string eogJson, string historiqueJson)
    {
        try
        {
            using var eog = JsonDocument.Parse(eogJson);
            using var historique = JsonDocument.Parse(historiqueJson);
            return Construire(eog.RootElement, historique.RootElement);
        }
        catch (JsonException)
        {
            return new ResultatRapport.Ignore("JSON illisible");
        }
    }

    private static string? RaisonDIgnorer(JsonElement eog)
    {
        if (eog.ValueKind != JsonValueKind.Object) return "bloc de fin de game inattendu";
        var type = Texte(eog, "gameType");
        if (type != "CUSTOM_GAME") return $"gameType {type ?? "absent"} : seules les customs sont rapportées";

        if (!eog.TryGetProperty("teams", out var equipes) || equipes.ValueKind != JsonValueKind.Array || equipes.GetArrayLength() != 2)
            return "il n'y a pas exactement deux équipes";

        var tailles = equipes.EnumerateArray().Select(Joueurs).Select(j => j.Count).ToList();
        if (tailles.Any(t => t != 5))
            return $"équipes de {string.Join(" et ", tailles)} joueurs : seules les 5v5 sont rapportées";

        return null;
    }

    private static ResultatRapport Construire(JsonElement eog, JsonElement historique)
    {
        var raison = RaisonDIgnorer(eog);
        if (raison is not null) return new ResultatRapport.Ignore(raison);

        if (!historique.TryGetProperty("mapId", out var map) || !map.TryGetInt32(out var mapId))
            return new ResultatRapport.Ignore("mapId absent de l'historique");
        if (mapId != MapFaille)
            return new ResultatRapport.Ignore($"mapId {mapId} : seule la Faille (11) est rapportée");

        var platformId = Texte(historique, "platformId");
        if (string.IsNullOrEmpty(platformId)) return new ResultatRapport.Ignore("platformId absent de l'historique");
        if (!eog.TryGetProperty("gameId", out var gameId) || !gameId.TryGetInt64(out var id))
            return new ResultatRapport.Ignore("gameId absent du bloc de fin de game");

        var joueurs = new List<JoueurDuRapport>();
        foreach (var equipe in eog.GetProperty("teams").EnumerateArray())
        {
            var victoire = equipe.TryGetProperty("isWinningTeam", out var v) && v.ValueKind == JsonValueKind.True;
            foreach (var joueur in Joueurs(equipe))
            {
                var side = joueur.TryGetProperty("teamId", out var t) && t.TryGetInt32(out var teamId)
                    ? teamId switch { 100 => "blue", 200 => "red", _ => null }
                    : null;
                if (side is null) return new ResultatRapport.Ignore("teamId inattendu (ni 100 ni 200)");
                if (!joueur.TryGetProperty("championId", out var champion) || !champion.TryGetInt32(out var championId))
                    return new ResultatRapport.Ignore("championId absent");
                if (!joueur.TryGetProperty("stats", out var stats) || stats.ValueKind != JsonValueKind.Object)
                    return new ResultatRapport.Ignore("stats absentes");

                var puuid = Texte(joueur, "puuid");
                joueurs.Add(new JoueurDuRapport(string.IsNullOrEmpty(puuid) ? null : puuid, side, championId, victoire, StatsEnrichies(joueur, stats)));
            }
        }

        return new ResultatRapport.Construit(new RapportDeGame($"{platformId}_{id}", Donnees(eog, historique, platformId), joueurs));
    }

    /// <summary>Les stats du client, plus ce que le client met à côté d'elles : rôle joué, objets, sorts (ADR 0021). Rien n'est inventé : une absence reste une absence.</summary>
    private static JsonElement StatsEnrichies(JsonElement joueur, JsonElement stats)
    {
        var enrichies = JsonNode.Parse(stats.GetRawText())!.AsObject();
        // Le rôle des stats prime ; le rôle détecté ne comble qu'une clé absente ou vide.
        if (enrichies["teamPosition"] is not JsonValue { } position || position.GetValueKind() != JsonValueKind.String
            || string.IsNullOrEmpty(position.GetValue<string>()))
            CopierSous(joueur, enrichies, "detectedTeamPosition", "teamPosition");
        CopierSous(joueur, enrichies, "items", "items");
        CopierSous(joueur, enrichies, "spell1Id", "spell1Id");
        CopierSous(joueur, enrichies, "spell2Id", "spell2Id");
        return JsonSerializer.SerializeToElement(enrichies);
    }

    private static void CopierSous(JsonElement source, JsonObject cible, string cle, string nom)
    {
        if (source.TryGetProperty(cle, out var valeur) && valeur.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            cible[nom] = JsonNode.Parse(valeur.GetRawText());
    }

    private static readonly string[] Objectifs =
        ["dragonKills", "baronKills", "riftHeraldKills", "hordeKills", "towerKills", "inhibitorKills", "firstBlood", "firstTower", "firstInhibitor"];

    /// <summary>Objectifs de chaque équipe, lus dans l'historique : liste blanche, un objet par teamId.</summary>
    private static JsonArray? ObjectifsDesEquipes(JsonElement historique)
    {
        if (!historique.TryGetProperty("teams", out var equipes) || equipes.ValueKind != JsonValueKind.Array) return null;
        var resultat = new JsonArray();
        foreach (var equipe in equipes.EnumerateArray())
        {
            if (equipe.ValueKind != JsonValueKind.Object || !equipe.TryGetProperty("teamId", out var teamId) || !teamId.TryGetInt32(out _)) continue;
            var objet = new JsonObject();
            Copier(equipe, objet, ["teamId", .. Objectifs]);
            resultat.Add(objet);
        }
        return resultat.Count == 0 ? null : resultat;
    }

    /// <summary>Liste blanche : jamais de copie du bloc (jetons JWT, mot de passe de chat et XP n'ont rien à faire dans le Rapport).</summary>
    private static JsonObject Donnees(JsonElement eog, JsonElement historique, string platformId)
    {
        var donnees = new JsonObject();
        Copier(eog, donnees, "gameId", "gameLength", "gameMode", "gameType", "queueType", "endOfGameTimestamp");
        Copier(historique, donnees, "gameVersion", "mapId", "queueId");
        donnees["platformId"] = platformId;
        var equipes = ObjectifsDesEquipes(historique);
        if (equipes is not null) donnees["teams"] = equipes;
        return donnees;
    }

    private static void Copier(JsonElement source, JsonObject cible, params string[] cles)
    {
        foreach (var cle in cles)
            if (source.TryGetProperty(cle, out var valeur) && valeur.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                cible[cle] = JsonNode.Parse(valeur.GetRawText());
    }

    private static List<JsonElement> Joueurs(JsonElement equipe) =>
        equipe.ValueKind == JsonValueKind.Object && equipe.TryGetProperty("players", out var j) && j.ValueKind == JsonValueKind.Array
            ? j.EnumerateArray().ToList()
            : [];

    private static string? Texte(JsonElement objet, string cle) =>
        objet.ValueKind == JsonValueKind.Object && objet.TryGetProperty(cle, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
