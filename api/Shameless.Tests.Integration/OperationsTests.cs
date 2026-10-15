using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class OperationsTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task The_health_check_is_anonymous_and_includes_the_database()
    {
        var response = await Factory.CreateAnonymousClient().GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task A_missing_resource_is_reported_as_problem_details()
    {
        var response = await Factory.CreateSignedInClient().GetAsync("/series/999", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task A_missing_session_is_reported_as_problem_details()
    {
        var response = await Factory.CreateAnonymousClient().GetAsync("/players", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task An_unexpected_error_returns_problem_details_without_technical_detail()
    {
        // Compte Riot inconnu : le service lève une exception non gérée.
        var response = await Factory.CreateSignedInClient().PostAsJsonAsync("/players", new { prenom = "X", riotId = "Inconnu#EUW" }, Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("   at ", body);
    }

    [Fact]
    public async Task The_openapi_document_is_not_exposed_outside_development()
    {
        var response = await Factory.CreateSignedInClient().GetAsync("/openapi/v1.json", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_openapi_document_is_exposed_in_development()
    {
        await using var development = Factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));

        var response = await development.CreateClient().GetAsync("/openapi/v1.json", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(document.GetProperty("paths").TryGetProperty("/players", out _));
    }

    [Fact]
    public void Without_a_riot_key_the_rank_refresh_job_is_disabled_with_a_log()
    {
        var logs = Factory.Services.GetRequiredService<FakeLogCollector>().GetSnapshot();

        Assert.Contains(logs, log => log.Message.Contains("rank refresh disabled", StringComparison.OrdinalIgnoreCase));
    }
}
