using Shameless.Application.Discord;

namespace Shameless.Tests.Unit;

/// <summary>Un ID Discord (snowflake) se lit et s'écrit en texte, et n'est jamais nul ni négatif.</summary>
public class DiscordIdTests
{
    [Fact]
    public void Un_snowflake_en_texte_se_lit_et_se_reecrit_a_l_identique()
    {
        var id = DiscordId.Lire("900000000000000001");

        Assert.Equal(900000000000000001L, id?.Valeur);
        Assert.Equal("900000000000000001", id.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData(" 1")]
    [InlineData("1e3")]
    [InlineData("abc")]
    [InlineData("99999999999999999999")]
    public void Ce_qui_n_est_pas_un_snowflake_ne_se_lit_pas(string? texte)
    {
        Assert.Null(DiscordId.Lire(texte));
    }
}
