using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Tmdb;
using MovieFlix.Api.Models;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Tests;

/// <summary>
/// Unit tests for the shared <see cref="MovieCacheService"/> using a fake
/// <see cref="ITmdbService"/> so no real TMDB calls are made.
/// </summary>
public class MovieCacheServiceTests
{
    private static async Task<(SqliteConnection connection, MovieFlixDbContext context)>
        CreateContextAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MovieFlixDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new MovieFlixDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return (connection, context);
    }

    [Fact]
    public async Task GetOrCreateByTmdbIdAsync_WhenNotCached_FetchesFromTmdbAndStores()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var fake = new FakeTmdbService
        {
            Details = new TmdbMovieDetailsResponse
            {
                Id = 157336,
                Title = "Interstellar",
                Overview = "Space."
            }
        };

        var service = new MovieCacheService(context, fake);

        var movie = await service.GetOrCreateByTmdbIdAsync(157336);

        Assert.Equal(157336, movie.TmdbId);
        Assert.Equal("Interstellar", movie.Title);
        Assert.Equal(1, fake.DetailsCallCount);

        var stored = await context.Movies.SingleAsync();
        Assert.Equal(movie.Id, stored.Id);
    }

    [Fact]
    public async Task GetOrCreateByTmdbIdAsync_WhenAlreadyCached_DoesNotCallTmdb()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var cached = new Movie { TmdbId = 550, Title = "Fight Club", Overview = "" };
        context.Movies.Add(cached);
        await context.SaveChangesAsync();

        var fake = new FakeTmdbService();
        var service = new MovieCacheService(context, fake);

        var movie = await service.GetOrCreateByTmdbIdAsync(550);

        Assert.Equal(cached.Id, movie.Id);
        Assert.Equal(0, fake.DetailsCallCount);
        Assert.Equal(1, await context.Movies.CountAsync());
    }

    [Fact]
    public async Task GetOrCreateByTmdbIdAsync_CalledTwice_CreatesSingleRow()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var fake = new FakeTmdbService
        {
            Details = new TmdbMovieDetailsResponse
            {
                Id = 603,
                Title = "The Matrix",
                Overview = ""
            }
        };

        var service = new MovieCacheService(context, fake);

        var first = await service.GetOrCreateByTmdbIdAsync(603);
        var second = await service.GetOrCreateByTmdbIdAsync(603);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await context.Movies.CountAsync());
        // The second call hits the fast path and does not re-fetch from TMDB.
        Assert.Equal(1, fake.DetailsCallCount);
    }

    private sealed class FakeTmdbService : ITmdbService
    {
        public int DetailsCallCount { get; private set; }

        public TmdbMovieDetailsResponse Details { get; set; } = new();

        public Task<TmdbMovieSearchResponse> SearchMoviesAsync(
            string query, int page, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TmdbMovieDetailsResponse> GetMovieDetailsAsync(
            int tmdbId, CancellationToken cancellationToken = default)
        {
            DetailsCallCount++;
            return Task.FromResult(Details);
        }
    }
}
