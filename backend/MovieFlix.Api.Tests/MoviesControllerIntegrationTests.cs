// Note: using statements are in the file content; we just need to ensure correct namespaces
using System;
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
using MovieFlix.Api.DTOs.Movies;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Tests;

public class MoviesControllerIntegrationTests
{
    [Fact]
    public async Task GetMovie_WhenMovieDoesNotExist_Returns404ProblemDetails()
    {
        using var factory = new MovieFlixApiFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var response = await client.GetAsync("/api/v1/Movies/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content
            .ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal("Movie not found.", problem.Title);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task GetMovie_WhenMovieExists_Returns200AndMovieData()
    {
        using var factory = new MovieFlixApiFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
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

        var response = await client.GetAsync($"/api/v1/Movies/{movie.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<MovieResponse>();

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
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var response = await client.GetAsync($"/api/v1/Movies/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal("Invalid movie ID.", problem.Title);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task GetMyFavorites_WhenNotAuthenticated_Returns401Unauthorized()
    {
        using var factory = new MovieFlixApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me/favorites");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyFavorites_WhenAuthenticated_Returns200OK()
    {
        using var factory = new MovieFlixApiFactory();
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
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

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/Auth/login",
            new { email = user.Email, password });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginData);
        Assert.False(string.IsNullOrWhiteSpace(loginData.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginData.AccessToken);

        var favoritesResponse = await client.GetAsync("/api/v1/me/favorites");
        Assert.Equal(HttpStatusCode.OK, favoritesResponse.StatusCode);
    }
}
