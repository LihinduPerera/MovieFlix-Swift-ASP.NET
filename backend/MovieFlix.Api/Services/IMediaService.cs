using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>TMDB-backed discovery, details, seasons and per-title state.</summary>
    public interface IMediaService
    {
        Task<PagedResponse<MediaSummaryResponse>> SearchAsync(
            SearchQueryRequest request,
            CancellationToken cancellationToken = default);

        Task<PagedResponse<MediaSummaryResponse>> GetTrendingAsync(
            TrendingQueryRequest request,
            CancellationToken cancellationToken = default);

        Task<PagedResponse<MediaSummaryResponse>> GetFeedAsync(
            MediaType mediaType,
            MediaFeed feed,
            MediaListQueryRequest request,
            CancellationToken cancellationToken = default);

        Task<PagedResponse<MediaSummaryResponse>> DiscoverAsync(
            MediaType mediaType,
            DiscoverQueryRequest request,
            CancellationToken cancellationToken = default);

        Task<MediaDetailsResponse?> GetDetailsAsync(
            MediaType mediaType,
            int tmdbId,
            string? language,
            CancellationToken cancellationToken = default);

        Task<List<SeasonSummaryResponse>> GetSeasonsAsync(
            int tmdbId,
            string? language,
            CancellationToken cancellationToken = default);

        Task<SeasonDetailsResponse?> GetSeasonDetailsAsync(
            int tmdbId,
            int seasonNumber,
            string? language,
            string? userId,
            CancellationToken cancellationToken = default);

        Task<MediaStateResponse> GetStateAsync(
            MediaType mediaType,
            int tmdbId,
            string? userId,
            CancellationToken cancellationToken = default);

        Task<List<GenreResponse>> GetGenresAsync(
            MediaType mediaType,
            string? language,
            CancellationToken cancellationToken = default);
    }
}
