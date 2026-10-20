using Shameless.Application.Entities;
using Shameless.Application.Services;

namespace Shameless.Tests.Unit;

/// <summary>La validation d'une demande de correction de stats : la première erreur ne dépend pas de l'ordre du dictionnaire.</summary>
public class CorrectionDeStatsValidationTests
{
    // La validation précède tout accès aux dépôts : une demande invalide n'en touche aucun.
    private static readonly CorrectionDeStatsService Service = new(null!, null!, TimeProvider.System);

    private static async Task<string?> Detail(params (ChampCorrigeable Champ, object Valeur)[] champs)
    {
        var demande = new DemandeDeCorrectionDeStats(champs.ToDictionary(c => c.Champ, c => c.Valeur));
        var resultat = await Service.CorrigerAsync(1, 1, demande);
        Assert.Equal(CommandStatus.Invalid, resultat.Status);
        return resultat.Detail;
    }

    [Fact]
    public async Task La_premiere_erreur_est_celle_du_premier_champ_dans_l_ordre_de_l_enumeration()
    {
        var kills = (ChampCorrigeable.Kills, (object)(-1));
        var deaths = (ChampCorrigeable.Deaths, (object)"beaucoup");
        var assists = (ChampCorrigeable.Assists, (object)99999);

        var attendu = await Detail(kills, deaths, assists);

        Assert.Contains("Kills", attendu, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(attendu, await Detail(assists, deaths, kills));
        Assert.Equal(attendu, await Detail(deaths, kills, assists));
    }
}
