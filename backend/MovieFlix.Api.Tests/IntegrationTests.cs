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

public class MoviesControllerIntegrationTests2 : IDisposable
{
    private readonly MovieFlixApiFactory _factory;
    private readonly HttpClient _client;

    public MoviesControllerIntegrationTests2()
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
    public async Task GetMovie_WhenMovieDoesNotExist_Returns404ProblemDetails()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var response = await _client.GetAsync("/api/v1/Movies/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Movie not found.", problem.Title);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task GetMovie_WhenMovieExists_Returns200AndMovieData()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var movie = new Movie
        {
            TmdbId = 157336,
            Title = "Interstellar",
            Overview = "A team of explorers travels through a wormhole."
        };

        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/v1/Movies/{movie.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<MovieFlix.Api.DTOs.Movies.MovieResponse>();
        Assert.NotNull(result);
        Assert.Equal(movie.Id, result.Id);
        Assert.Equal(157336, result.TmdbId);
        Assert.Equal("Interstellar", result.Title);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetMovie_WhenIdIsNotPositive_Returns400BadRequest(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var response = await _client.GetAsync($"/api/v1/Movies/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Invalid movie ID.", problem.Title);
        Assert.Equal(400, problem.Status);
        Assert.Equal("Movie ID must be greater than zero.", problem.Detail);
    }

    [Fact]
    public async Task GetMyFavorites_WhenNotAuthenticated_Returns401Unauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var response = await _client.GetAsync("/api/v1/me/favorites");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyFavorites_WhenAuthenticated_Returns200OK()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await dbContext.Database.EnsureCreatedAsync();

        const string password = "TestPassword123!";
        var user = new ApplicationUser
        {
            UserName = "integration-test@example.com",
            Email = "integration-test@example.com",
            NormalizedEmail = "INTEGRATION-TEST@EXAMPLE.COM",
            NormalizedUserName = "INTEGRATION-TEST@EXAMPLE.COM",
            DisplayName = "Integration Test User",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await userManager.CreateAsync(user, password);
        await userManager.AddToRoleAsync(user, "User");

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/v1/Auth/login",
            new LoginRequest { Email = user.Email!, Password = password });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginData);
        Assert.False(string.IsNullOrWhiteSpace(loginData.AccessToken));

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginData.AccessToken);

        var favoritesResponse = await _client.GetAsync("/api/v1/me/favorites");
        Assert.Equal(HttpStatusCode.OK, favoritesResponse.StatusCode);
    }

    [Fact]
    public async Task GetMyFavorites_WhenAuthenticated_ReturnsOnlyCurrentUsersFavorites()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await dbContext.Database.EnsureCreatedAsync();

        const string password = "TestPassword123!";

        var alice = new ApplicationUser
        {
            UserName = "alice-test@example.com",
            Email = "alice-test@example.com",
            NormalizedEmail = "ALICE-TEST@EXAMPLE.COM",
            NormalizedUserName = "ALICE-TEST@EXAMPLE.COM",
            DisplayName = "Alice",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var bob = new ApplicationUser
        {
            UserName = "bob-test@example.com",
            Email = "bob-test@example.com",
            NormalizedEmail = "BOB-TEST@EXAMPLE.COM",
            NormalizedUserName = "BOB-TEST@EXAMPLE.COM",
            DisplayName = "Bob",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await userManager.CreateAsync(alice, password);
        await userManager.CreateAsync(bob, password);
        await userManager.AddToRolesAsync(alice, new[] { "User" });
        await userManager.AddToRolesAsync(bob, new[] { "User" });

        var aliceMovie = new Movie { TmdbId = 157336, Title = "Alice's Interstellar", Overview = "Alice's favorite movie." };
        var bobMovie = new Movie { TmdbId = 550, Title = "Bob's Fight Club", Overview = "Bob's favorite movie." };

        dbContext.Movies.AddRange(aliceMovie, bobMovie);
        await dbContext.SaveChangesAsync();

        dbContext.Favorites.AddRange(
            new Favorite { UserId = alice.Id, MovieId = aliceMovie.Id, CreatedAt = DateTime.UtcNow },
            new Favorite { UserId = bob.Id, MovieId = bobMovie.Id, CreatedAt = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/Auth/login", new { email = alice.Email, password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginResult);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.AccessToken);

        var favoritesResponse = await _client.GetAsync("/api/v1/me/favorites");
        Assert.Equal(HttpStatusCode.OK, favoritesResponse.StatusCode);
        var favorites = await favoritesResponse.Content.ReadFromJsonAsync<List<FavoriteMovieResponse>>();
        Assert.NotNull(favorites);

        var favorite = Assert.Single(favorites);
        Assert.Equal(aliceMovie.Id, favorite.Id);
        Assert.Equal(aliceMovie.TmdbId, favorite.TmdbId);
        Assert.Equal("Alice's Interstellar", favorite.Title);
        Assert.DoesNotContain(favorites, f => f.Title == "Bob's Fight Club");
    }

    [Theory]
    [InlineData("alice-test@example.com", "WrongPassword123!")]
    [InlineData("missing@example.com", "TestPassword123!")]
    public async Task Login_WhenCredentialsAreInvalid_Returns401Unauthorized(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await dbContext.Database.EnsureCreatedAsync();

        const string correctPassword = "TestPassword123!";
        var user = new ApplicationUser
        {
            UserName = "alice-test@example.com",
            Email = "alice-test@example.com",
            NormalizedEmail = "ALICE-TEST@EXAMPLE.COM",
            NormalizedUserName = "ALICE-TEST@EXAMPLE.COM",
            DisplayName = "Alice",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await userManager.CreateAsync(user, correctPassword);
        await userManager.AddToRoleAsync(user, "User");

        var response = await _client.PostAsJsonAsync("/api/v1/Auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
