using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieFlix.Api.Data;

namespace MovieFlix.Api.Tests;

/// <summary>
/// Integration tests covering the cross-cutting infrastructure added for
/// production readiness: health checks and API version reporting.
/// </summary>
public class CrossCuttingIntegrationTests : IDisposable
{
    private readonly MovieFlixApiFactory _factory;
    private readonly HttpClient _client;

    public CrossCuttingIntegrationTests()
    {
        _factory = new MovieFlixApiFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Health_WhenDatabaseIsReachable_ReturnsHealthy()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task VersionedEndpoint_AdvertisesSupportedApiVersions()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var movie = new MovieFlix.Api.Models.Movie
        {
            TmdbId = 157336,
            Title = "Interstellar",
            Overview = "Space."
        };
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/v1/Movies/{movie.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.TryGetValues("api-supported-versions", out var versions),
            "Expected the api-supported-versions response header.");

        Assert.Contains("1.0", versions!);
    }

    [Fact]
    public async Task UnauthenticatedRequest_ToProtectedEndpoint_Returns401()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var response = await _client.GetAsync("/api/v1/me/watchlist");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthEndpoint_AfterExceedingRateLimit_Returns429()
    {
        // Override the (relaxed) test limit to prove the "auth" policy is
        // actually enforced. The limiter short-circuits before the action
        // runs, so no database or real credentials are required.
        using var limitedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:AuthPermitLimit", "2");
        });
        using var client = limitedFactory.CreateClient();

        HttpStatusCode? lastStatus = null;
        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/Auth/login",
                new { email = "nobody@example.com", password = "Password123!" });

            lastStatus = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }
}
