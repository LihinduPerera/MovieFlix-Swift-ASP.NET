using MovieFlix.Api.DTOs.Tmdb;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>
    /// Owns the local TMDB metadata cache. Public identities are always
    /// (mediaType, tmdbId); database ids never leave this layer.
    /// </summary>
    public interface IMediaCacheService
    {
        /// <summary>
        /// Returns the cached title, fetching and persisting TMDB details on a
        /// miss. Safe under concurrent requests for the same title.
        /// </summary>
        Task<MediaItem> GetOrCreateAsync(
            MediaType mediaType,
            int tmdbId,
            string language,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts or updates a title from already-fetched TMDB details. Used by
        /// the details endpoint so a request never calls TMDB twice.
        /// </summary>
        Task<MediaItem> UpsertAsync(
            MediaType mediaType,
            TmdbMediaDetailsResponse details,
            CancellationToken cancellationToken = default);

        /// <summary>Forces a metadata refresh from TMDB.</summary>
        Task<MediaItem> RefreshAsync(
            MediaType mediaType,
            int tmdbId,
            string language,
            CancellationToken cancellationToken = default);

        /// <summary>Fetches and caches all episodes for one season if needed.</summary>
        Task EnsureSeasonEpisodesAsync(
            int mediaItemId,
            int seasonNumber,
            string language,
            CancellationToken cancellationToken = default);

        /// <summary>Resolves one cached episode, caching its season if necessary.</summary>
        Task<TvEpisode?> GetOrCreateEpisodeAsync(
            int mediaItemId,
            int seasonNumber,
            int episodeNumber,
            string language,
            CancellationToken cancellationToken = default);
    }
}
