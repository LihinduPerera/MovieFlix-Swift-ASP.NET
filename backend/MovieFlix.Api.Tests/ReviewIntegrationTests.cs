using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Auth;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Reviews;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Tests;

public class ReviewIntegrationTests : IDisposable
{
    private readonly MovieFlixApiFactory _factory;
    private readonly HttpClient _client;

    public ReviewIntegrationTests()
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
        IServiceScope scope, string email, string displayName)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        const string password = "TestPassword123!";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            DisplayName = displayName,
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
    public async Task CreateReview_ThenList_ReturnsReview()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "reviewer@example.com", "Reviewer");

        var movie = new Movie { TmdbId = 157336, Title = "Interstellar", Overview = "" };
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("reviewer@example.com", "TestPassword123!");
        Authorize(login);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/v1/movies/{movie.Id}/reviews",
            new CreateReviewRequest { Content = "A masterpiece." });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ReviewResponse>();
        Assert.NotNull(created);
        Assert.Equal("A masterpiece.", created.Content);
        Assert.Equal("Reviewer", created.AuthorDisplayName);

        // Public listing works even for anonymous callers.
        _client.DefaultRequestHeaders.Authorization = null;
        var listResponse = await _client.GetAsync($"/api/v1/movies/{movie.Id}/reviews");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<PagedResponse<ReviewResponse>>();
        Assert.NotNull(page);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal("A masterpiece.", page.Items[0].Content);
    }

    [Fact]
    public async Task CreateReview_Twice_Returns409()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "dup@example.com", "Dup");

        var movie = new Movie { TmdbId = 1, Title = "Dup Movie", Overview = "" };
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("dup@example.com", "TestPassword123!");
        Authorize(login);

        await _client.PostAsJsonAsync($"/api/v1/movies/{movie.Id}/reviews",
            new CreateReviewRequest { Content = "First." });

        var second = await _client.PostAsJsonAsync(
            $"/api/v1/movies/{movie.Id}/reviews",
            new CreateReviewRequest { Content = "Second." });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task UpdateReview_WhenNotOwner_Returns403()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var (ownerId, _) = await CreateUserAsync(scope, "owner@example.com", "Owner");
        await CreateUserAsync(scope, "intruder@example.com", "Intruder");

        var movie = new Movie { TmdbId = 2, Title = "Owned", Overview = "" };
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        var review = new Review
        {
            UserId = ownerId,
            MovieId = movie.Id,
            Content = "Owner's review.",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("intruder@example.com", "TestPassword123!");
        Authorize(login);

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/reviews/{review.Id}",
            new UpdateReviewRequest { Content = "Hacked!" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAndDelete_ByOwner_Works()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var (ownerId, password) = await CreateUserAsync(scope, "self@example.com", "Self");

        var movie = new Movie { TmdbId = 3, Title = "Self Movie", Overview = "" };
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        var review = new Review
        {
            UserId = ownerId,
            MovieId = movie.Id,
            Content = "Original.",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("self@example.com", password);
        Authorize(login);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1/reviews/{review.Id}",
            new UpdateReviewRequest { Content = "Edited." });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ReviewResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Edited.", updated.Content);

        var deleteResponse = await _client.DeleteAsync($"/api/v1/reviews/{review.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var count = await dbContext.Reviews.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetMyReviews_ReturnsOnlyOwnReviews()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var (aliceId, alicePassword) = await CreateUserAsync(scope, "alice-rev@example.com", "Alice");
        var (bobId, _) = await CreateUserAsync(scope, "bob-rev@example.com", "Bob");

        var movie = new Movie { TmdbId = 9, Title = "Shared", Overview = "" };
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        dbContext.Reviews.AddRange(
            new Review { UserId = aliceId, MovieId = movie.Id, Content = "Alice says hi.", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Review { UserId = bobId, MovieId = movie.Id, Content = "Bob says hi.", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("alice-rev@example.com", alicePassword);
        Authorize(login);

        var response = await _client.GetAsync("/api/v1/me/reviews");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mine = await response.Content.ReadFromJsonAsync<List<ReviewResponse>>();
        Assert.NotNull(mine);
        var only = Assert.Single(mine);
        Assert.Equal("Alice says hi.", only.Content);
    }
}
