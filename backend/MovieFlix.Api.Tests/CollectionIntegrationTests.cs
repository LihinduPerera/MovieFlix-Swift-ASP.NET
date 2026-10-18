using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Auth;
using MovieFlix.Api.DTOs.Collections;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Tests;

public class CollectionIntegrationTests : IDisposable
{
    private readonly MovieFlixApiFactory _factory;
    private readonly HttpClient _client;

    public CollectionIntegrationTests()
    {
        _factory = new MovieFlixApiFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<string> CreateUserAsync(IServiceScope scope, string email)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        const string password = "TestPassword123!";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            DisplayName = "Collector",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await userManager.CreateAsync(user, password);
        await userManager.AddToRoleAsync(user, "User");
        return user.Id;
    }

    private async Task<LoginResponse> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/Auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result);
        return result;
    }

    private void Authorize(LoginResponse login) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

    [Fact]
    public async Task CreateCollection_ThenAddAndRemoveMovies_Works()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "collector@example.com");

        var movie = new Movie { TmdbId = 157336, Title = "Interstellar", Overview = "" };
        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("collector@example.com", "TestPassword123!");
        Authorize(login);

        // Create
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/me/collections",
            new CreateCollectionRequest { Name = "Sci-Fi", Description = "Best sci-fi" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CollectionSummaryResponse>();
        Assert.NotNull(created);
        Assert.Equal(0, created.MovieCount);

        // Add movie
        var addResponse = await _client.PostAsync(
            $"/api/v1/me/collections/{created.Id}/movies/{movie.Id}", null);
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);
        var detail = await addResponse.Content.ReadFromJsonAsync<CollectionDetailResponse>();
        Assert.NotNull(detail);
        var only = Assert.Single(detail.Movies);
        Assert.Equal("Interstellar", only.Title);

        // Add duplicate -> 409
        var dupResponse = await _client.PostAsync(
            $"/api/v1/me/collections/{created.Id}/movies/{movie.Id}", null);
        Assert.Equal(HttpStatusCode.Conflict, dupResponse.StatusCode);

        // List shows the count
        var listResponse = await _client.GetAsync("/api/v1/me/collections");
        var list = await listResponse.Content.ReadFromJsonAsync<List<CollectionSummaryResponse>>();
        Assert.NotNull(list);
        Assert.Equal(1, Assert.Single(list).MovieCount);

        // Remove movie
        var removeResponse = await _client.DeleteAsync(
            $"/api/v1/me/collections/{created.Id}/movies/{movie.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        // Delete collection
        var deleteResponse = await _client.DeleteAsync($"/api/v1/me/collections/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateCollection_WhenNotOwner_Returns403()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var ownerId = await CreateUserAsync(scope, "owner-col@example.com");
        await CreateUserAsync(scope, "intruder-col@example.com");

        var collection = new Collection
        {
            UserId = ownerId,
            Name = "Private",
            Description = "",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Collections.Add(collection);
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync("intruder-col@example.com", "TestPassword123!");
        Authorize(login);

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/me/collections/{collection.Id}",
            new UpdateCollectionRequest { Name = "Hijacked", Description = "" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCollection_IsPublicRead()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var ownerId = await CreateUserAsync(scope, "public-col@example.com");

        var collection = new Collection
        {
            UserId = ownerId,
            Name = "Public List",
            Description = "Anyone can read",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Collections.Add(collection);
        await dbContext.SaveChangesAsync();

        // No auth header.
        var response = await _client.GetAsync($"/api/v1/collections/{collection.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<CollectionDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Public List", detail.Name);
    }
}
