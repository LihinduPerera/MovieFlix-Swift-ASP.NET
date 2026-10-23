using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Auth;
using MovieFlix.Api.DTOs.Favorites;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Tests;

public class WatchlistIntegrationTests : IDisposable
{
    private readonly MovieFlixApiFactory _factory;
    private readonly HttpClient _client;

    public WatchlistIntegrationTests()
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
        IServiceScope scope,
        string email)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var password = "TestPassword123!";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            DisplayName = "Watchlist Tester",
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
    public async Task GetWatchlist_WhenNotAuthenticated_Returns401()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var response = await _client.GetAsync("/api/v1/me/watchlist");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddAndRemoveWatchlistItem_Works()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var (userId, password) = await CreateUserAsync(scope, "watchlist-test@example.com");

        var movie = new Movie { TmdbId = 157336, Title = "Interstellar", Overview = "Space." };
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("watchlist-test@example.com", password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Add
        var addResponse = await _client.PostAsync($"/api/v1/me/watchlist/{movie.Id}", null);
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);

        // List
        var listResponse = await _client.GetAsync("/api/v1/me/watchlist");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var items = await listResponse.Content.ReadFromJsonAsync<List<FavoriteMovieResponse>>();
        Assert.NotNull(items);
        var item = Assert.Single(items);
        Assert.Equal("Interstellar", item.Title);

        // Add duplicate -> 409
        var duplicateResponse = await _client.PostAsync($"/api/v1/me/watchlist/{movie.Id}", null);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        // Remove
        var removeResponse = await _client.DeleteAsync($"/api/v1/me/watchlist/{movie.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        // List empty
        var emptyResponse = await _client.GetAsync("/api/v1/me/watchlist");
        var emptyItems = await emptyResponse.Content.ReadFromJsonAsync<List<FavoriteMovieResponse>>();
        Assert.NotNull(emptyItems);
        Assert.Empty(emptyItems);
    }

    [Fact]
    public async Task Watchlist_IsIsolatedPerUser()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var (aliceId, alicePassword) = await CreateUserAsync(scope, "alice-watch@example.com");
        var (bobId, bobPassword) = await CreateUserAsync(scope, "bob-watch@example.com");

        var aliceMovie = new Movie { TmdbId = 100, Title = "Alice Movie", Overview = "" };
        var bobMovie = new Movie { TmdbId = 200, Title = "Bob Movie", Overview = "" };
        dbContext.Movies.AddRange(aliceMovie, bobMovie);
        await dbContext.SaveChangesAsync();

        dbContext.WatchlistItems.AddRange(
            new WatchlistItem { UserId = aliceId, MovieId = aliceMovie.Id, AddedAt = DateTime.UtcNow },
            new WatchlistItem { UserId = bobId, MovieId = bobMovie.Id, AddedAt = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var aliceLogin = await LoginAsync("alice-watch@example.com", alicePassword);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aliceLogin.AccessToken);

        var aliceResponse = await _client.GetAsync("/api/v1/me/watchlist");
        var aliceItems = await aliceResponse.Content.ReadFromJsonAsync<List<FavoriteMovieResponse>>();
        Assert.NotNull(aliceItems);
        Assert.Single(aliceItems);
        Assert.Equal("Alice Movie", aliceItems[0].Title);
    }
}
