using Shameless.Application.Services;

namespace Shameless.Tests.Unit;

public class MmrCostTests
{
    [Theory]
    [InlineData("IRON", 4, 800)]
    [InlineData("BRONZE", 4, 1000)]
    [InlineData("SILVER", 4, 1200)]
    [InlineData("GOLD", 4, 1450)]
    [InlineData("PLATINUM", 4, 1700)]
    [InlineData("EMERALD", 4, 2000)]
    [InlineData("DIAMOND", 4, 2300)]
    public void Each_tier_starts_at_its_base_mmr_in_division_IV(string tier, int division, int expected)
    {
        Assert.Equal(expected, MMRCalculationService.GetRankMMR(tier, division));
    }

    [Theory]
    [InlineData(4, 1450)]
    [InlineData(3, 1475)]
    [InlineData(2, 1500)]
    [InlineData(1, 1525)]
    public void Each_division_above_IV_adds_25_mmr(int division, int expected)
    {
        Assert.Equal(expected, MMRCalculationService.GetRankMMR("GOLD", division));
    }

    [Theory]
    [InlineData("MASTER", 2600)]
    [InlineData("GRANDMASTER", 2900)]
    [InlineData("CHALLENGER", 3200)]
    public void Master_and_above_ignore_the_division(string tier, int expected)
    {
        Assert.Equal(expected, MMRCalculationService.GetRankMMR(tier, 1));
        Assert.Equal(expected, MMRCalculationService.GetRankMMR(tier, null));
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("UNRANKED", 0)]
    [InlineData("gold", 1450)]
    public void Missing_or_lowercase_tiers_are_handled(string? tier, int expected)
    {
        Assert.Equal(expected, MMRCalculationService.GetRankMMR(tier, null));
    }

    [Theory]
    [InlineData(-100, 1)]
    [InlineData(0, 1)]
    [InlineData(1199, 1)]
    [InlineData(1200, 2)]
    [InlineData(1599, 2)]
    [InlineData(1600, 3)]
    [InlineData(1999, 3)]
    [InlineData(2000, 4)]
    [InlineData(2399, 4)]
    [InlineData(2400, 5)]
    [InlineData(2799, 5)]
    [InlineData(2800, 6)]
    [InlineData(3275, 6)]
    public void Mmr_maps_to_a_cost_between_1_and_6(int mmr, int expected)
    {
        Assert.Equal((decimal)expected, MMRCalculationService.MMRToCost(mmr));
    }
}
