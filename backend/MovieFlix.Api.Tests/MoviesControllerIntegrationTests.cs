
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Favorites;
using MovieFlix.Api.DTOs.Movies;
using MovieFlix.Api.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MovieFlix.Api.Tests;

public class MoviesControllerIntegrationTests
{
    [Fact]
    public async Task GetMovie_WhenMovieDoesNotExist_Returns404ProblemDetails()
    {
        // Arrange: Start the API with isolated test database.
        using var factory = new MovieFlixApiFactory();
        using var client = factory.CreateClient();

        // Create the database schema for this test.
        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<MovieFlixDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        // Act: Request a movie ID that does not exist.
        var response = await client.GetAsync("/api/v1/Movies/999");

        // Assert: Verify the HTTP status code.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Read the response body as Problem Details.
        var problem = await response.Content
            .ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal("Movie not found.", problem.Title);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task GetMovie_WhenMovieExists_Returns200AndMovieData()
    {
        // Arrange: Start the API with an isolated database.
        using var factory = new MovieFlixApiFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<MovieFlixDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        // Insert a movie directly into the test database.
        var movie = new MovieFlix.Api.Models.Movie
        {
            TmdbId = 157336,
            Title = "Interstellar",
            Overview = "A team of explorers travels through a wormhole."
        };

        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync();

        // Act: Request the movie through the HTTP API.
        var response = await client.GetAsync(
            $"/api/v1/Movies/{movie.Id}");

        // Assert: The API should return 200 OK.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify the JSON response contains the correct movie.
        var result = await response.Content
            .ReadFromJsonAsync<MovieResponse>();

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
        using var factory = new MovieFlixApiFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<MovieFlixDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        var response = await client.GetAsync($"/api/v1/Movies/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content
            .ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal("Invalid movie ID.", problem.Title);
        Assert.Equal(400, problem.Status);
        Assert.Equal(
            "Movie ID must be greater than zero.",
            problem.Detail);
    }


    [Fact]
    public async Task GetMyFavorites_WhenNotAuthenticated_Returns401Unauthorized()
    {
        // Arrange: Start the API with our isolated test host.
        using var factory = new MovieFlixApiFactory();
        using var client = factory.CreateClient();

        // Act: Send a request without an Authorization header.
        var response = await client.GetAsync("/api/v1/me/favorites");

        // Assert: Protected endpoints must reject anonymous requests.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyFavorites_WhenAuthenticated_Returns200OK()
    {
        // Arrange: Start the API and its isolated test database.
        using var factory = new MovieFlixApiFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<MovieFlixDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        // Create a test user with a properly hashed password.
        var passwordHasher =
            scope.ServiceProvider
                .GetRequiredService<
                    Microsoft.AspNetCore.Identity.IPasswordHasher<
                        MovieFlix.Api.Models.User>>();

        var user = new MovieFlix.Api.Models.User
        {
            Email = "integration-test@example.com",
            DisplayName = "Integration Test User",
            Role = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.PasswordHash = passwordHasher.HashPassword(
            user,
            "TestPassword123!");

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Act 1: Log in through the real API.
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/Auth/login",
            new
            {
                email = user.Email,
                password = "TestPassword123!"
            });

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode);

        var loginData = await loginResponse.Content
            .ReadFromJsonAsync<
                MovieFlix.Api.DTOs.Auth.LoginResponse>();

        Assert.NotNull(loginData);
        Assert.False(
            string.IsNullOrWhiteSpace(loginData.AccessToken));

        // Act 2: Use the real JWT to access the protected endpoint.
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                loginData.AccessToken);

        var favoritesResponse = await client.GetAsync(
            "/api/v1/me/favorites");

        // Assert: The authenticated request succeeds.
        Assert.Equal(
            HttpStatusCode.OK,
            favoritesResponse.StatusCode);
    }

    [Fact]
    public async Task GetMyFavorites_WhenAuthenticated_ReturnsOnlyCurrentUsersFavorites()
    {
        // Arrange: Start the API and isolated test database.
        using var factory = new MovieFlixApiFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<MovieFlixDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        // Create valid password hashes using the application's password hasher.
        var passwordHasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<User>>();

        const string password = "TestPassword123!";

        var alice = new User
        {
            Email = "alice-test@example.com",
            DisplayName = "Alice",
            Role = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        alice.PasswordHash = passwordHasher.HashPassword(alice, password);

        var bob = new User
        {
            Email = "bob-test@example.com",
            DisplayName = "Bob",
            Role = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        bob.PasswordHash = passwordHasher.HashPassword(bob, password);

        dbContext.Users.AddRange(alice, bob);

        var aliceMovie = new Movie
        {
            TmdbId = 157336,
            Title = "Alice's Interstellar",
            Overview = "Alice's favorite movie."
        };

        var bobMovie = new Movie
        {
            TmdbId = 550,
            Title = "Bob's Fight Club",
            Overview = "Bob's favorite movie."
        };

        dbContext.Movies.AddRange(aliceMovie, bobMovie);

        // Save users and movies first so their IDs are generated.
        await dbContext.SaveChangesAsync();

        // Give each user a different favorite.
        dbContext.Favorites.AddRange(
            new Favorite
            {
                UserId = alice.Id,
                MovieId = aliceMovie.Id,
                CreatedAt = DateTime.UtcNow
            },
            new Favorite
            {
                UserId = bob.Id,
                MovieId = bobMovie.Id,
                CreatedAt = DateTime.UtcNow
            });

        await dbContext.SaveChangesAsync();

        // Log in as Alice through the real API.
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/Auth/login",
            new
            {
                email = alice.Email,
                password
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginResult = await loginResponse.Content
            .ReadFromJsonAsync<MovieFlix.Api.DTOs.Auth.LoginResponse>();

        Assert.NotNull(loginResult);
        Assert.False(string.IsNullOrWhiteSpace(loginResult.AccessToken));

        // Send Alice's JWT with the favorites request.
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                loginResult.AccessToken);

        // Act: Request the authenticated user's favorites.
        var favoritesResponse = await client.GetAsync(
            "/api/v1/me/favorites");

        // Assert: The request succeeds.
        Assert.Equal(HttpStatusCode.OK, favoritesResponse.StatusCode);

        var favorites = await favoritesResponse.Content
            .ReadFromJsonAsync<List<FavoriteMovieResponse>>();

        Assert.NotNull(favorites);

        // Alice must receive exactly her own favorite.
        var favorite = Assert.Single(favorites);

        Assert.Equal(aliceMovie.Id, favorite.Id);
        Assert.Equal(aliceMovie.TmdbId, favorite.TmdbId);
        Assert.Equal("Alice's Interstellar", favorite.Title);

        // Bob's movie must not appear in Alice's response.
        Assert.DoesNotContain(
            favorites,
            movie => movie.Title == "Bob's Fight Club");
    }

    [Theory]
    [InlineData("alice-test@example.com", "WrongPassword123!")]
    [InlineData("missing@example.com", "TestPassword123!")]
    public async Task Login_WhenCredentialsAreInvalid_Returns401Unauthorized(
    string email,
    string password)
    {
        // Arrange: Start the API with an isolated database.
        using var factory = new MovieFlixApiFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<MovieFlixDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        // Create one real user with a valid password hash.
        var passwordHasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<User>>();

        const string correctPassword = "TestPassword123!";

        var user = new User
        {
            Email = "alice-test@example.com",
            DisplayName = "Alice",
            Role = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.PasswordHash = passwordHasher.HashPassword(
            user,
            correctPassword);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Act: Attempt to log in with invalid credentials.
        var response = await client.PostAsJsonAsync(
            "/api/v1/Auth/login",
            new
            {
                email,
                password
            });

        // Assert: Both cases must be rejected.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Register_WhenEmailAlreadyExists_Returns409Conflict()
    {
        // Arrange: Start the API with an isolated database.
        using var factory = new MovieFlixApiFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<MovieFlixDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        var registration = new
        {
            email = "alice-test@example.com",
            displayName = "Alice",
            password = "TestPassword123!"
        };

        // Act 1: Register the user for the first time.
        var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/Auth/register",
            registration);

        // Assert: The first registration should succeed.
        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        // Act 2: Attempt to register the same email again,
        // using uppercase letters to test case-insensitive handling.
        var duplicateRegistration = new
        {
            email = "ALICE-TEST@EXAMPLE.COM",
            displayName = "Another Alice",
            password = "AnotherPassword123!"
        };

        var duplicateResponse = await client.PostAsJsonAsync(
            "/api/v1/Auth/register",
            duplicateRegistration);

        // Assert: The API should return 409 Conflict.
        Assert.Equal(
            HttpStatusCode.Conflict,
            duplicateResponse.StatusCode);

        var problem = await duplicateResponse.Content
            .ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal("User already exists.", problem.Title);
        Assert.Equal(409, problem.Status);

        // Verify that only one account was created.
        var userCount = await dbContext.Users
            .CountAsync(user =>
                user.Email == "alice-test@example.com");

        Assert.Equal(1, userCount);
    }

    [Theory]
    [InlineData("not-an-email", "Alice", "TestPassword123!")]
    [InlineData("", "Alice", "TestPassword123!")]
    [InlineData("alice@example.com", "Alice", "short")]
    [InlineData("alice@example.com", "", "TestPassword123!")]
    public async Task Register_WhenRequestIsInvalid_Returns400BadRequest(
    string email,
    string displayName,
    string password)
    {
        // Arrange: Start the API with an isolated test database.
        using var factory = new MovieFlixApiFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<MovieFlixDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        // Act: Send an invalid registration request.
        var response = await client.PostAsJsonAsync(
            "/api/v1/Auth/register",
            new
            {
                email,
                displayName,
                password
            });

        // Assert: ASP.NET Core model validation rejects the request.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        // Invalid requests must not create accounts.
        var userCount = await dbContext.Users.CountAsync();

        Assert.Equal(0, userCount);
    }

    [Fact]
    public async Task AddFavorite_WhenMovieIsAlreadyFavorited_Returns409Conflict()
    {
        // Arrange: Start the API and isolated test database.
        using var factory = new MovieFlixApiFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<MovieFlixDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        // Create a test user with a valid password hash.
        var passwordHasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<User>>();

        const string password = "TestPassword123!";

        var user = new User
        {
            Email = "favorite-test@example.com",
            DisplayName = "Favorite Test User",
            Role = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.PasswordHash = passwordHasher.HashPassword(user, password);

        var movie = new Movie
        {
            TmdbId = 157336,
            Title = "Interstellar",
            Overview = "A team of explorers travels through a wormhole."
        };

        dbContext.Users.Add(user);
        dbContext.Movies.Add(movie);

        await dbContext.SaveChangesAsync();

        // Log in through the real API.
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/Auth/login",
            new
            {
                email = user.Email,
                password
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginResult = await loginResponse.Content
            .ReadFromJsonAsync<MovieFlix.Api.DTOs.Auth.LoginResponse>();

        Assert.NotNull(loginResult);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                loginResult.AccessToken);

        // First request: Add the favorite.
        var firstResponse = await client.PostAsync(
            $"/api/v1/me/favorites/{movie.Id}",
            content: null);

        Assert.True(
            firstResponse.IsSuccessStatusCode,
            $"First request failed with {(int)firstResponse.StatusCode}.");

        // Second request: Try adding the same favorite again.
        var duplicateResponse = await client.PostAsync(
            $"/api/v1/me/favorites/{movie.Id}",
            content: null);

        // Assert: The duplicate should return 409 Conflict.
        Assert.Equal(
            HttpStatusCode.Conflict,
            duplicateResponse.StatusCode);

        var problem = await duplicateResponse.Content
            .ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal("Favorite already exists.", problem.Title);
        Assert.Equal(409, problem.Status);

        // Confirm the database still has only one favorite.
        var favoriteCount = await dbContext.Favorites.CountAsync(
            favorite => favorite.UserId == user.Id
                && favorite.MovieId == movie.Id);

        Assert.Equal(1, favoriteCount);
    }
}
