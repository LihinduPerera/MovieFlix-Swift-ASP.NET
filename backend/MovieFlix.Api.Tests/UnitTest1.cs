
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.Models;
using MovieFlix.Api.Services;
using Xunit;

namespace MovieFlix.Api.Tests;

public class MovieServiceTests
{
    [Fact]
    public async Task GetMovieByIdAsync_WhenMovieExists_ReturnsMovieResponse()
    {
        // ARRANGE: Prepare a temporary database and test data.
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MovieFlixDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new MovieFlixDbContext(options);

        await context.Database.EnsureCreatedAsync();

        var movie = new Movie
        {
            TmdbId = 157336,
            Title = "Interstellar",
            Overview = "A team of explorers travels through a wormhole in space."
        };

        context.Movies.Add(movie);
        await context.SaveChangesAsync();

        // ACT: Call the real MovieFlix service.
        var movieService = new MovieService(context);

        var result = await movieService.GetMovieByIdAsync(movie.Id);

        // ASSERT: Verify the result.
        Assert.NotNull(result);
        Assert.Equal(movie.Id, result.Id);
        Assert.Equal(157336, result.TmdbId);
        Assert.Equal("Interstellar", result.Title);
    }

    
    [Fact]
    public async Task GetMovieByIdAsync_WhenMovieDoesNotExist_ReturnsNull()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MovieFlixDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new MovieFlixDbContext(options);

        await context.Database.EnsureCreatedAsync();

        var movieService = new MovieService(context);

        var result = await movieService.GetMovieByIdAsync(999);

        Assert.Null(result);
    }
}
