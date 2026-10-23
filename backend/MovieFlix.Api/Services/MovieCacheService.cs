using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>
    /// Default <see cref="IMovieCacheService"/>. Implements the same
    /// "check → fetch TMDB outside the transaction → re-check inside the
    /// transaction → insert" pattern that was previously duplicated across the
    /// favorite and watchlist services.
    /// </summary>
    public class MovieCacheService : IMovieCacheService
    {
        private readonly MovieFlixDbContext _context;
        private readonly ITmdbService _tmdbService;

        public MovieCacheService(
            MovieFlixDbContext context,
            ITmdbService tmdbService)
        {
            _context = context;
            _tmdbService = tmdbService;
        }

        public async Task<Movie> GetOrCreateByTmdbIdAsync(
            int tmdbId, CancellationToken cancellationToken = default)
        {
            // Fast path: the movie is already cached locally.
            var movie = await _context.Movies
                .SingleOrDefaultAsync(
                    m => m.TmdbId == tmdbId,
                    cancellationToken);

            if (movie is not null)
            {
                return movie;
            }

            // Slow path: fetch details from TMDB *before* opening a database
            // transaction so we never hold a transaction open during network I/O.
            var details = await _tmdbService.GetMovieDetailsAsync(
                tmdbId,
                cancellationToken);

            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            // Re-check inside the transaction: another request may have inserted
            // the same movie while we were calling TMDB.
            movie = await _context.Movies
                .SingleOrDefaultAsync(
                    m => m.TmdbId == tmdbId,
                    cancellationToken);

            if (movie is null)
            {
                movie = new Movie
                {
                    TmdbId = details.Id,
                    Title = details.Title,
                    Overview = details.Overview
                };

                _context.Movies.Add(movie);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return movie;
        }
    }
}
