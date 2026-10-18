using System.Net;
using System.Net.Http.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>Chaque erreur métier est un ProblemDetails avec le bon statut.</summary>
public class ErrorTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Linking_an_unknown_player_returns_404()
    {
        var response = await Factory.CreateSignedInClient().PostAsJsonAsync("/players/link", new { playerId = 999 }, Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Linking_a_player_already_linked_returns_a_409_problem()
    {
        var owner = Factory.CreateSignedInClient("8001");
        var playerId = await CreatePlayerAsync(owner, "Pris");
        await ReadJsonAsync(await owner.PostAsJsonAsync("/players/link", new { playerId }, Ct));

        var response = await Factory.CreateSignedInClient("8002").PostAsJsonAsync("/players/link", new { playerId }, Ct);

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Setting_your_peak_without_a_linked_player_returns_404()
    {
        var response = await Factory.CreateSignedInClient("8003").PostAsJsonAsync("/players/peak",
            new { peakTier = "GOLD", peakDivision = 1, peakSeason = "S25", peakLp = 0 }, Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Your_player_without_a_link_is_a_404_problem()
    {
        var response = await Factory.CreateSignedInClient("8004").GetAsync("/players/me", Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unknown_player_is_a_404_problem()
    {
        var response = await Factory.CreateSignedInClient().GetAsync("/players/999", Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Previewing_an_unknown_riot_account_is_a_404_problem()
    {
        var response = await Factory.CreateSignedInClient().GetAsync("/players/riot-preview?riotId=Personne%23EUW", Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
        Assert.Contains("Compte Riot introuvable", await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData(6.4)]
    [InlineData(0.6)]
    public async Task A_fractional_cost_outside_1_to_6_is_rejected(double cost)
    {
        var admin = Factory.CreateAdminClient();
        var playerId = await CreatePlayerAsync(admin, "Cible");

        var response = await admin.PutAsJsonAsync($"/players/{playerId}/cost", new { cost }, Ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_refused_return_url_is_a_400_problem()
    {
        var response = await Factory.CreateAnonymousClient().GetAsync("/auth/discord/login?returnUrl=https%3A%2F%2Fevil.example", Ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Signing_in_from_outside_the_guild_is_a_403_problem()
    {
        var client = Factory.CreateAnonymousClient();
        var code = Factory.Discord.AddUser("8005", "Intrus", "other-guild");

        var response = await TestSession.CompleteLoginAsync(client, code);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("\"error\"", body);
    }
}
