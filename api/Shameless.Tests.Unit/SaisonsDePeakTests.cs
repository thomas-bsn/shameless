using Shameless.Application.Joueurs;

namespace Shameless.Tests.Unit;

public class SaisonsDePeakTests
{
    [Fact]
    public void Les_saisons_vont_de_S24_a_la_saison_de_l_annee_civile()
    {
        Assert.Equal(["S24", "S25", "S26"], SaisonsDePeak.Jusqua(2026));
        Assert.Equal("S26", SaisonsDePeak.Courante(2026));
    }

    [Fact]
    public void En_2024_seule_S24_existe()
    {
        Assert.Equal(["S24"], SaisonsDePeak.Jusqua(2024));
    }

    [Theory]
    [InlineData("S24", 2026, true)]
    [InlineData("S26", 2026, true)]
    [InlineData("S23", 2026, false)]
    [InlineData("S27", 2026, false)]
    [InlineData("S2025", 2026, false)]
    [InlineData("s25", 2026, false)]
    [InlineData("25", 2026, false)]
    [InlineData("", 2026, false)]
    [InlineData(null, 2026, false)]
    public void Une_saison_est_valide_dans_la_plage(string? saison, int annee, bool attendu)
    {
        Assert.Equal(attendu, SaisonsDePeak.EstValide(saison, annee));
    }
}
