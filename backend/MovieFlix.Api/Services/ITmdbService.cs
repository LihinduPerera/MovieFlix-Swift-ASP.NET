using MovieFlix.Api.DTOs.Tmdb;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>
    /// Thin, normalized abstraction over the TMDB HTTP API. All upstream
    /// shapes are converted here; callers never see raw TMDB payloads.
    /// </summary>
    public interface ITmdbService
    {
        Task<TmdbSearchResponse> SearchAsync(
            string query,
            SearchType type,
            int page,
            string language,
            string? region,
            CancellationToken cancellationToken = default);

        Task<TmdbMediaDetailsResponse> GetDetailsAsync(
            MediaType mediaType,
            int tmdbId,
            string language,
            CancellationToken cancellationToken = default);

        Task<TmdbSearchResponse> GetTrendingAsync(
            MediaTypeFilter type,
            TrendingWindow window,
            int page,
            string language,
            CancellationToken cancellationToken = default);

        Task<TmdbSearchResponse> GetFeedAsync(
            MediaType mediaType,
            MediaFeed feed,
            int page,
            string language,
            string? region,
            CancellationToken cancellationToken = default);

        Task<TmdbSeasonDetailsResponse> GetSeasonAsync(
            int tvId,
            int seasonNumber,
            string language,
            CancellationToken cancellationToken = default);

        Task<List<TmdbGenre>> GetGenresAsync(
            MediaType mediaType,
            string language,
            CancellationToken cancellationToken = default);

        Task<TmdbSearchResponse> GetDiscoverAsync(
            MediaType mediaType,
            int? genreId,
            int? year,
            double? minRating,
            string? sortBy,
            int page,
            string language,
            string? region,
            CancellationToken cancellationToken = default);
    }
}
