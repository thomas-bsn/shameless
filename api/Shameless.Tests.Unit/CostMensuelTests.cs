using Shameless.Application.Cost;

namespace Shameless.Tests.Unit;

public class CostMensuelTests
{
    private static readonly CostDeDepart Trois = CostDeDepart.DuMoisPrecedent(3m);

    private static GameDuMois[] Games(int nombre, bool gagnee = true, int? note = 50) =>
        Enumerable.Range(0, nombre).Select(_ => new GameDuMois(gagnee, note)).ToArray();

    [Fact]
    public void Avant_cinq_games_terminees_le_cost_reste_celui_de_depart()
    {
        var cost = CostMensuel.Calculer(Trois, Games(4, gagnee: true, note: 100));

        Assert.Equal(3m, cost.Cost);
        Assert.False(cost.Evolue);
        Assert.Equal(4, cost.GamesTerminees);
        Assert.Equal(4, cost.Victoires);
    }

    [Fact]
    public void A_partir_de_cinq_games_le_cost_combine_la_note_moyenne_et_le_winrate()
    {
        // Note moyenne 70, 4 victoires sur 5 (80 %).
        GameDuMois[] games = [new(true, 60), new(true, 80), new(true, 70), new(true, 65), new(false, 75)];

        var cost = CostMensuel.Calculer(Trois, games);

        // Performance : 0,7 × (70 − 50) / 50 = +0,28 ; résultats : 0,3 × (80 % − 50 %) / 50 % = +0,18.
        Assert.True(cost.Evolue);
        Assert.Equal(70, cost.NoteMoyenne);
        Assert.Equal(80, cost.Winrate);
        Assert.Equal(0.28m, cost.AjustementPerformance);
        Assert.Equal(0.18m, cost.AjustementResultats);
        Assert.Equal(3.5m, cost.Cost); // 3,46 arrondi au dixième
    }

    [Fact]
    public void La_performance_individuelle_pese_plus_que_le_winrate()
    {
        // Bonnes notes dans une équipe qui perd, contre mauvaises notes portées par une équipe qui gagne.
        var performant = CostMensuel.Calculer(Trois, Games(5, gagnee: false, note: 80));
        var porte = CostMensuel.Calculer(Trois, Games(5, gagnee: true, note: 20));

        // +0,42 − 0,30 = +0,12 → 3,1 ; −0,42 + 0,30 = −0,12 → 2,9.
        Assert.Equal(3.1m, performant.Cost);
        Assert.Equal(2.9m, porte.Cost);
    }

    [Fact]
    public void Une_game_moyenne_avec_la_moitie_de_victoires_ne_change_pas_le_cost()
    {
        GameDuMois[] games = [new(true, 50), new(false, 50), new(true, 50), new(false, 50), new(true, 50), new(false, 50)];

        Assert.Equal(3m, CostMensuel.Calculer(Trois, games).Cost);
    }

    [Theory]
    [InlineData(5.5, true, 100, 6)]   // 5,5 + 1 plafonné à 6
    [InlineData(1.5, false, 0, 1)]    // 1,5 − 1 plancher à 1
    [InlineData(3, true, 100, 4)]     // variation maximale d'un mois : +1
    [InlineData(3, false, 0, 2)]      // et −1
    public void Le_cost_varie_d_au_plus_un_point_par_mois_et_reste_entre_1_et_6(double depart, bool gagnee, int note, double attendu)
    {
        var cost = CostMensuel.Calculer(CostDeDepart.DuMoisPrecedent((decimal)depart), Games(5, gagnee, note));

        Assert.Equal((decimal)attendu, cost.Cost);
    }

    [Fact]
    public void Sans_aucune_note_de_performance_le_cost_n_evolue_pas()
    {
        // Games saisies à la main : pas de stats, donc pas de note. Le winrate seul ne fait pas
        // évoluer le cost, même avec 5 victoires sur 5.
        var cost = CostMensuel.Calculer(Trois, Games(5, gagnee: true, note: null));

        Assert.Null(cost.NoteMoyenne);
        Assert.Equal(0, cost.GamesNotees);
        Assert.False(cost.Evolue);
        Assert.Equal(0m, cost.AjustementPerformance);
        Assert.Equal(0m, cost.AjustementResultats);
        Assert.Equal(3m, cost.Cost);
    }

    [Theory]
    [InlineData("IRON", 1, 1)]
    [InlineData("SILVER", 4, 2)]
    [InlineData("GOLD", 2, 2)]
    [InlineData("PLATINUM", 4, 3)]
    [InlineData("EMERALD", 4, 4)]
    [InlineData("DIAMOND", 1, 4)]
    [InlineData("MASTER", null, 5)]
    [InlineData("CHALLENGER", null, 6)]
    [InlineData("UNRANKED", null, 1)]
    [InlineData(null, null, 1)]
    public void Sans_historique_le_rang_soloq_donne_le_cost_de_depart(string? tier, int? division, int attendu)
    {
        var depart = CostDeDepart.DuRangSoloQ(tier, division);

        Assert.Equal(attendu, depart.Cost);
        Assert.Equal(SourceDuCostDeDepart.RangSoloQ, depart.Source);
    }

    [Fact]
    public void La_note_moyenne_ne_compte_que_les_games_notees()
    {
        GameDuMois[] games = [new(false, 90), new(false, 90), new(false, null), new(false, null), new(false, null)];

        var cost = CostMensuel.Calculer(Trois, games);

        // Note moyenne 90 sur 2 games : +0,56 ; 0 % de victoires : −0,30.
        Assert.Equal(90, cost.NoteMoyenne);
        Assert.Equal(2, cost.GamesNotees);
        Assert.Equal(3.3m, cost.Cost);
    }
}
