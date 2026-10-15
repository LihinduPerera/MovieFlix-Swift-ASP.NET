using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Auth;
using MovieFlix.Api.DTOs.SearchHistory;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Tests;

public class SearchHistoryIntegrationTests : IDisposable
{
    private readonly MovieFlixApiFactory _factory;
    private readonly HttpClient _client;

    public SearchHistoryIntegrationTests()
    {
        _factory = new MovieFlixApiFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<(string userId, string password)> CreateUserAsync(
        IServiceScope scope, string email)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        const string password = "TestPassword123!";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            DisplayName = "Search Tester",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await userManager.CreateAsync(user, password);
        await userManager.AddToRoleAsync(user, "User");
        return (user.Id, password);
    }

    private async Task<LoginResponse> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/Auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result);
        return result;
    }

    private void Authorize(LoginResponse login) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

    [Fact]
    public async Task Record_RepeatSearch_DeduplicatesCaseInsensitively()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "search@example.com");

        var login = await LoginAsync("search@example.com", "TestPassword123!");
        Authorize(login);

        await _client.PostAsJsonAsync("/api/v1/me/search-history",
            new RecordSearchRequest { Query = "Inception" });
        await _client.PostAsJsonAsync("/api/v1/me/search-history",
            new RecordSearchRequest { Query = "  INCEPTION  " });

        var count = await dbContext.SearchHistoryEntries.CountAsync();
        Assert.Equal(1, count);

        var entry = await dbContext.SearchHistoryEntries.FirstAsync();
        // Latest casing/trim wins.
        Assert.Equal("INCEPTION", entry.Query);
    }

    [Fact]
    public async Task GetRecent_ReturnsNewestFirst_AndRespectsLimit()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "recent-search@example.com");

        var login = await LoginAsync("recent-search@example.com", "TestPassword123!");
        Authorize(login);

        await _client.PostAsJsonAsync("/api/v1/me/search-history",
            new RecordSearchRequest { Query = "First" });
        await _client.PostAsJsonAsync("/api/v1/me/search-history",
            new RecordSearchRequest { Query = "Second" });
        await _client.PostAsJsonAsync("/api/v1/me/search-history",
            new RecordSearchRequest { Query = "Third" });

        var response = await _client.GetAsync("/api/v1/me/search-history?limit=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<SearchHistoryResponse>>();
        Assert.NotNull(items);
        Assert.Equal(2, items.Count);
        Assert.Equal("Third", items[0].Query);
    }

    [Fact]
    public async Task RemoveAndClear_Work()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "clear-search@example.com");

        var login = await LoginAsync("clear-search@example.com", "TestPassword123!");
        Authorize(login);

        var first = await _client.PostAsJsonAsync("/api/v1/me/search-history",
            new RecordSearchRequest { Query = "Alpha" });
        var created = await first.Content.ReadFromJsonAsync<SearchHistoryResponse>();
        Assert.NotNull(created);

        await _client.PostAsJsonAsync("/api/v1/me/search-history",
            new RecordSearchRequest { Query = "Beta" });

        // Remove one.
        var removeResponse = await _client.DeleteAsync(
            $"/api/v1/me/search-history/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        // Removing again -> 404.
        var againResponse = await _client.DeleteAsync(
            $"/api/v1/me/search-history/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, againResponse.StatusCode);

        // Clear the rest.
        var clearResponse = await _client.DeleteAsync("/api/v1/me/search-history");
        Assert.Equal(HttpStatusCode.NoContent, clearResponse.StatusCode);

        var count = await dbContext.SearchHistoryEntries.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task SearchHistory_IsIsolatedPerUser()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var (aliceId, alicePassword) = await CreateUserAsync(scope, "alice-search@example.com");
        var (bobId, _) = await CreateUserAsync(scope, "bob-search@example.com");

        dbContext.SearchHistoryEntries.AddRange(
            new SearchHistoryEntry { UserId = aliceId, Query = "Alice Term", NormalizedQuery = "ALICE TERM", SearchedAt = DateTime.UtcNow },
            new SearchHistoryEntry { UserId = bobId, Query = "Bob Term", NormalizedQuery = "BOB TERM", SearchedAt = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("alice-search@example.com", alicePassword);
        Authorize(login);

        var response = await _client.GetAsync("/api/v1/me/search-history");
        var items = await response.Content.ReadFromJsonAsync<List<SearchHistoryResponse>>();
        Assert.NotNull(items);
        var only = Assert.Single(items);
        Assert.Equal("Alice Term", only.Query);
    }

    [Fact]
    public async Task SearchHistory_WhenNotAuthenticated_Returns401()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var response = await _client.GetAsync("/api/v1/me/search-history");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
