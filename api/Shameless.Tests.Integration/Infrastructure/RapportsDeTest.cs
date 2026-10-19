namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>Les rapports de fin de game que les tests font envoyer par le Companion.</summary>
public static class RapportsDeTest
{
    public static readonly string[] ChampionsA = ["Garen", "LeeSin", "Ahri", "Jinx", "Thresh"];
    public static readonly string[] ChampionsB = ["Darius", "Zed", "Orianna", "Ezreal", "Nami"];

    /// <summary>L'id numérique que le client LoL donne au champion (sa clé Riot), pour son id Data Dragon.</summary>
    public static int IdClient(string championDataDragon) =>
        int.Parse(FakeChampionCatalog.Champions.Single(c => c.Id == championDataDragon).Key);

    /// <summary>
    /// Le rapport d'une game où l'Équipe A (prénoms <paramref name="equipeA"/>, A1..A5 par défaut)
    /// joue <paramref name="teamASide"/> contre l'Équipe B (<paramref name="equipeB"/>, B1..B5).
    /// Chaque joueur joue le champion de même rang dans <paramref name="champions"/> (A puis B), par
    /// défaut <see cref="ChampionsA"/> puis <see cref="ChampionsB"/>. Le rapport est vu par le joueur
    /// <paramref name="emetteur"/> : ses stats et ses données diffèrent d'un émetteur à l'autre, comme
    /// deux Companions qui n'envoient pas exactement les mêmes données. <paramref name="statsDe"/>
    /// remplace les stats du joueur de rang i (A puis B), <paramref name="donnees"/> celles de la partie.
    /// </summary>
    public static object Rapport(
        string riotMatchId,
        string teamASide,
        string gagnante,
        string[]? equipeA = null,
        string[]? equipeB = null,
        string[]? champions = null,
        int emetteur = 0,
        Func<int, object>? statsDe = null,
        object? donnees = null)
    {
        equipeA ??= ["A1", "A2", "A3", "A4", "A5"];
        equipeB ??= ["B1", "B2", "B3", "B4", "B5"];
        champions ??= [.. ChampionsA, .. ChampionsB];
        var teamBSide = teamASide == "blue" ? "red" : "blue";
        var joueurs = equipeA.Select(p => (Prenom: p, Side: teamASide, Victoire: gagnante == "A"))
            .Concat(equipeB.Select(p => (Prenom: p, Side: teamBSide, Victoire: gagnante == "B")))
            .Select((j, i) => new
            {
                puuid = $"puuid-{j.Prenom}#EUW",
                side = j.Side,
                championId = IdClient(champions[i]),
                victoire = j.Victoire,
                stats = statsDe?.Invoke(i) ?? new { kills = i + emetteur, deaths = 2, assists = 7, totalDamageDealtToChampions = 12000 + i }
            })
            .ToArray();
        return new
        {
            riotMatchId,
            donnees = donnees ?? new { gameDuration = 1834 + emetteur, gameMode = "CLASSIC", gameType = "CUSTOM_GAME" },
            joueurs
        };
    }
}
