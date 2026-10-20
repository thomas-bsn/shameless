using Shameless.Application.Stats;

namespace Shameless.Tests.Unit;

/// <summary>Les seuils de lettre de l'Impact (spec #196) : S+ ≥ 90, S ≥ 80, A ≥ 70, B ≥ 55, C ≥ 40, sinon D.</summary>
public class LettreDImpactTests
{
    [Theory]
    [InlineData(100, LettreDImpact.SPlus)]
    [InlineData(90, LettreDImpact.SPlus)]
    [InlineData(89, LettreDImpact.S)]
    [InlineData(80, LettreDImpact.S)]
    [InlineData(79, LettreDImpact.A)]
    [InlineData(70, LettreDImpact.A)]
    [InlineData(69, LettreDImpact.B)]
    [InlineData(55, LettreDImpact.B)]
    [InlineData(54, LettreDImpact.C)]
    [InlineData(40, LettreDImpact.C)]
    [InlineData(39, LettreDImpact.D)]
    [InlineData(0, LettreDImpact.D)]
    public void Chaque_score_tombe_dans_sa_lettre(int score, LettreDImpact attendue) =>
        Assert.Equal(attendue, ImpactDuJoueur.Lettre(score));
}
