using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Auth;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.History;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Tests;

public class WatchHistoryIntegrationTests : IDisposable
{
    private readonly MovieFlixApiFactory _factory;
    private readonly HttpClient _client;

    public WatchHistoryIntegrationTests()
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
            DisplayName = "History Tester",
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
            "/api/v1/Auth/login",
            new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result);
        return result;
    }

    [Fact]
    public async Task ContinueWatching_ReturnsOnlyUnfinishedStartedMovies()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "history@example.com");

        var started = new Movie { TmdbId = 1, Title = "Started", Overview = "" };
        var finished = new Movie { TmdbId = 2, Title = "Finished", Overview = "" };
        var untouched = new Movie { TmdbId = 3, Title = "Untouched", Overview = "" };
        dbContext.Movies.AddRange(started, finished, untouched);
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("history@example.com", "TestPassword123!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        await _client.PutAsJsonAsync($"/api/v1/me/history/{started.Id}",
            new RecordProgressRequest { ProgressSeconds = 120, Completed = false });
        await _client.PutAsJsonAsync($"/api/v1/me/history/{finished.Id}",
            new RecordProgressRequest { ProgressSeconds = 7200, Completed = true });

        var response = await _client.GetAsync("/api/v1/me/history/continue");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<WatchHistoryResponse>>();
        Assert.NotNull(items);
        var only = Assert.Single(items);
        Assert.Equal("Started", only.Title);
        Assert.Equal(120, only.ProgressSeconds);
    }

    [Fact]
    public async Task RecordProgress_IsUpserted_PerMovie()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "upsert@example.com");

        var movie = new Movie { TmdbId = 5, Title = "Upsert", Overview = "" };
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("upsert@example.com", "TestPassword123!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        await _client.PutAsJsonAsync($"/api/v1/me/history/{movie.Id}",
            new RecordProgressRequest { ProgressSeconds = 30, Completed = false });
        await _client.PutAsJsonAsync($"/api/v1/me/history/{movie.Id}",
            new RecordProgressRequest { ProgressSeconds = 90, Completed = false });

        var count = await dbContext.WatchHistoryEntries.CountAsync(
            e => e.MovieId == movie.Id);
        Assert.Equal(1, count);

        var entry = await dbContext.WatchHistoryEntries
            .FirstAsync(e => e.MovieId == movie.Id);
        Assert.Equal(90, entry.ProgressSeconds);
    }

    [Fact]
    public async Task GetRecentlyWatched_ReturnsPagedNewestFirst()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var (userId, password) = await CreateUserAsync(scope, "recent@example.com");

        var m1 = new Movie { TmdbId = 11, Title = "Older", Overview = "" };
        var m2 = new Movie { TmdbId = 12, Title = "Newer", Overview = "" };
        dbContext.Movies.AddRange(m1, m2);
        await dbContext.SaveChangesAsync();

        dbContext.WatchHistoryEntries.AddRange(
            new WatchHistoryEntry { UserId = userId, MovieId = m1.Id, WatchedAt = DateTime.UtcNow.AddHours(-2), ProgressSeconds = 10 },
            new WatchHistoryEntry { UserId = userId, MovieId = m2.Id, WatchedAt = DateTime.UtcNow, ProgressSeconds = 10 });
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("recent@example.com", password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var response = await _client.GetAsync("/api/v1/me/history?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WatchHistoryResponse>>();
        Assert.NotNull(page);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal("Newer", page.Items[0].Title);
    }

    [Fact]
    public async Task Clear_RemovesAllHistoryForUser()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "clear@example.com");

        var movie = new Movie { TmdbId = 20, Title = "Clear", Overview = "" };
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("clear@example.com", "TestPassword123!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        await _client.PutAsJsonAsync($"/api/v1/me/history/{movie.Id}",
            new RecordProgressRequest { ProgressSeconds = 10, Completed = false });

        var clearResponse = await _client.DeleteAsync("/api/v1/me/history");
        Assert.Equal(HttpStatusCode.NoContent, clearResponse.StatusCode);

        var count = await dbContext.WatchHistoryEntries.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task History_WhenNotAuthenticated_Returns401()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var response = await _client.GetAsync("/api/v1/me/history");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
