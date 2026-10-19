using System.Text.Json.Nodes;
using Spike;

namespace Spike.Tests;

public class AnonymizerTests
{
    private const string Eog = """
        {
          "gameId": 7123456789,
          "teams": [
            { "players": [
              { "summonerName": "Sam le Vrai", "puuid": "aaaa-bbbb-1111", "summonerId": 998877, "accountId": 5544, "championId": 64, "stats": { "KILLS": 3 } },
              { "summonerName": "Autre Joueur", "puuid": "cccc-dddd-2222", "summonerId": 112233, "championId": 1 }
            ] }
          ],
          "localPlayer": { "summonerName": "Sam le Vrai", "puuid": "aaaa-bbbb-1111" }
        }
        """;

    private const string History = """
        {
          "gameId": 7123456789,
          "participantIdentities": [
            { "participantId": 1, "player": { "gameName": "Sam le Vrai", "tagLine": "EUW", "puuid": "aaaa-bbbb-1111", "summonerName": "Sam le Vrai" } },
            { "participantId": 2, "player": { "gameName": "Autre Joueur", "tagLine": "1234", "puuid": "cccc-dddd-2222", "summonerName": "Autre Joueur" } }
          ]
        }
        """;

    [Fact]
    public void Remplace_pseudos_puuid_et_ids_de_compte_sans_toucher_aux_stats()
    {
        var result = Anonymizer.Anonymize([Eog, History]);

        var all = string.Concat(result);
        foreach (var secret in new[] { "Sam le Vrai", "Autre Joueur", "aaaa-bbbb-1111", "cccc-dddd-2222", "998877", "112233", "5544", "\"EUW\"", "\"1234\"" })
            Assert.DoesNotContain(secret, all);

        var eog = JsonNode.Parse(result[0])!;
        Assert.Equal(7123456789, (long)eog["gameId"]!);
        Assert.Equal(64, (int)eog["teams"]![0]!["players"]![0]!["championId"]!);
        Assert.Equal(3, (int)eog["teams"]![0]!["players"]![0]!["stats"]!["KILLS"]!);
    }

    [Fact]
    public void Garde_la_coherence_entre_fichiers_et_entre_joueurs()
    {
        var result = Anonymizer.Anonymize([Eog, History]);

        var eog = JsonNode.Parse(result[0])!;
        var history = JsonNode.Parse(result[1])!;
        var p1 = eog["teams"]![0]!["players"]![0]!;
        var p2 = eog["teams"]![0]!["players"]![1]!;

        Assert.Equal((string)p1["puuid"]!, (string)eog["localPlayer"]!["puuid"]!);
        Assert.Equal((string)p1["puuid"]!, (string)history["participantIdentities"]![0]!["player"]!["puuid"]!);
        Assert.Equal((string)p1["summonerName"]!, (string)history["participantIdentities"]![0]!["player"]!["gameName"]!);
        Assert.NotEqual((string)p1["puuid"]!, (string)p2["puuid"]!);
        Assert.NotEqual((string)p1["summonerName"]!, (string)p2["summonerName"]!);
    }

    [Fact]
    public void Remplace_aussi_un_pseudo_cache_dans_une_autre_chaine()
    {
        var json = """{"players":[{"summonerName":"Sam le Vrai"}],"message":"Sam le Vrai a gagné"}""";

        var result = Anonymizer.Anonymize([json]).Single();

        Assert.DoesNotContain("Sam le Vrai", result);
    }

    [Fact]
    public void Anonymise_un_dossier_de_captures_vers_un_autre()
    {
        var src = Path.Combine(Path.GetTempPath(), $"anon-src-{Guid.NewGuid():N}");
        var dst = Path.Combine(Path.GetTempPath(), $"anon-dst-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(src, "game-1"));
        File.WriteAllText(Path.Combine(src, "game-1", "eog-stats-block.json"), Eog);
        File.WriteAllText(Path.Combine(src, "game-1", "match-history-game.json"), History);
        File.WriteAllText(Path.Combine(src, "events-1.jsonl"), "{\"uri\":\"/x\"}\n");

        Anonymizer.AnonymizeDirectory(src, dst);

        var text = File.ReadAllText(Path.Combine(dst, "game-1", "eog-stats-block.json"));
        Assert.DoesNotContain("aaaa-bbbb-1111", text);
        Assert.True(File.Exists(Path.Combine(dst, "game-1", "match-history-game.json")));
        Assert.False(File.Exists(Path.Combine(dst, "events-1.jsonl")));
        Assert.Contains("Sam le Vrai", File.ReadAllText(Path.Combine(src, "game-1", "eog-stats-block.json")));
    }

    [Fact]
    public void Remplace_les_jetons_JWT_et_le_mot_de_passe_du_chat_de_fin_de_game()
    {
        const string jeton = "eyJraWQiOiIxIiwiYWxnIjoiUlMyNTYifQ.eyJzdWIiOiJ1bi1wdWF1aWQiLCJleHAiOjE3OTI0NTB9.c2lnbmF0dXJl";
        var json = $$"""
            { "gameId": 1, "mucJwtDto": { "jwt": "{{jeton}}", "domain": "lol-post-game" }, "multiUserChatPassword": "{{jeton}}" }
            """;

        var result = Anonymizer.Anonymize([json])[0];

        Assert.DoesNotContain("eyJ", result);
        Assert.Contains("lol-post-game", result);
        Assert.Contains("\"gameId\": 1", result);
    }
}
