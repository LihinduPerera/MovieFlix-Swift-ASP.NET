using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>
    /// Owns the local <see cref="Movie"/> cache that mirrors TMDB. Callers pass
    /// a TMDB id and get back a persisted local movie, fetched from TMDB only
    /// when it is not already cached.
    /// </summary>
    public interface IMovieCacheService
    {
        /// <summary>
        /// Returns the cached movie for a TMDB id, fetching its details from
        /// TMDB and storing them on first access. Safe under concurrent callers
        /// (the TMDB call happens outside the database transaction and the
        /// insert is re-checked inside it).
        /// </summary>
        /// <exception cref="Exceptions.TmdbServiceException">
        /// Thrown when TMDB cannot be reached or returns an error.
        /// </exception>
        Task<Movie> GetOrCreateByTmdbIdAsync(
            int tmdbId, CancellationToken cancellationToken = default);
    }
}
