using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Auth;
using MovieFlix.Api.DTOs.Ratings;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Tests;

public class RatingIntegrationTests : IDisposable
{
    private readonly MovieFlixApiFactory _factory;
    private readonly HttpClient _client;

    public RatingIntegrationTests()
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
            DisplayName = "Rating Tester",
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
    public async Task Rate_WhenMovieExists_CreatesAndUpdatesRating()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "rater@example.com");

        var movie = new Movie { TmdbId = 157336, Title = "Interstellar", Overview = "" };
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("rater@example.com", "TestPassword123!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Create a rating.
        var putResponse = await _client.PutAsJsonAsync(
            $"/api/v1/me/ratings/{movie.Id}",
            new RateMovieRequest { Rating = 5 });
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var created = await putResponse.Content.ReadFromJsonAsync<UserRatingResponse>();
        Assert.NotNull(created);
        Assert.Equal(5, created.Rating);

        // Update the same rating (upsert).
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1/me/ratings/{movie.Id}",
            new RateMovieRequest { Rating = 3 });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<UserRatingResponse>();
        Assert.NotNull(updated);
        Assert.Equal(3, updated.Rating);

        // Only one rating row exists.
        var ratingCount = await dbContext.MovieRatings.CountAsync(
            r => r.MovieId == movie.Id);
        Assert.Equal(1, ratingCount);
    }

    [Fact]
    public async Task Rate_WhenRatingIsOutOfRange_Returns400()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "range@example.com");

        var movie = new Movie { TmdbId = 1, Title = "Test", Overview = "" };
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("range@example.com", "TestPassword123!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/me/ratings/{movie.Id}",
            new { rating = 6 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CommunityRating_ComputesAverageAndCount()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var (aliceId, _) = await CreateUserAsync(scope, "alice-r@example.com");
        var (bobId, _) = await CreateUserAsync(scope, "bob-r@example.com");

        var movie = new Movie { TmdbId = 603, Title = "The Matrix", Overview = "" };
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        dbContext.MovieRatings.AddRange(
            new MovieRating { UserId = aliceId, MovieId = movie.Id, Rating = 5, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new MovieRating { UserId = bobId, MovieId = movie.Id, Rating = 4, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/v1/movies/{movie.Id}/ratings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var community = await response.Content.ReadFromJsonAsync<CommunityRatingResponse>();
        Assert.NotNull(community);
        Assert.Equal(4.5, community.AverageRating);
        Assert.Equal(2, community.RatingCount);
        Assert.Null(community.UserRating); // anonymous caller
    }

    [Fact]
    public async Task Remove_WhenRatingExists_Returns204()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "remover@example.com");

        var movie = new Movie { TmdbId = 2, Title = "Del", Overview = "" };
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("remover@example.com", "TestPassword123!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        await _client.PutAsJsonAsync(
            $"/api/v1/me/ratings/{movie.Id}",
            new RateMovieRequest { Rating = 4 });

        var deleteResponse = await _client.DeleteAsync($"/api/v1/me/ratings/{movie.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Removing again -> 404
        var againResponse = await _client.DeleteAsync($"/api/v1/me/ratings/{movie.Id}");
        Assert.Equal(HttpStatusCode.NotFound, againResponse.StatusCode);
    }
}
