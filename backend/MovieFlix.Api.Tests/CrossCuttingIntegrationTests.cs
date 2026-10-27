using System.Net;
using System.Net.Http.Json;

namespace MovieFlix.Api.Tests;

public sealed class CrossCuttingIntegrationTests : IntegrationTestBase
{
    public CrossCuttingIntegrationTests(MovieFlixApiFactory factory)
        : base(factory)
    {
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health")]
    public async Task HealthEndpoints_ReturnHealthy(string path)
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ApiVersion_IsReportedOnResponses()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/genres/movie");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "1.0",
            response.Headers.GetValues("api-supported-versions"));
    }

    [Fact]
    public async Task AdminEndpoint_WithRegularUserToken_ReturnsForbiddenProblem()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using var _client = client;

        var response = await client.PostAsync(
            "/api/v1/admin/media/movie/550/refresh",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(403, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task AuthRateLimit_RejectsRequestsBeyondConfiguredLimit()
    {
        using var limitedFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:AuthPermitLimit", "2");
            builder.UseSetting("RateLimiting:AuthWindowSeconds", "60");
        });

        using var client = limitedFactory.CreateClient();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var accepted = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new
                {
                    email = "nobody@example.com",
                    password = "Password1!"
                });

            Assert.Equal(HttpStatusCode.Unauthorized, accepted.StatusCode);
        }

        var rejected = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = "nobody@example.com",
                password = "Password1!"
            });

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);

        var problem = await ReadProblemAsync(rejected);
        Assert.Equal(429, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }
}
