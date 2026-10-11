
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Movies;

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
}
