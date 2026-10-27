using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.History;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>Movie progress, per-episode tracking, history and continue watching.</summary>
    public interface IWatchHistoryService
    {
        Task<WatchHistoryItemResponse> RecordProgressAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            RecordProgressRequest request,
            CancellationToken cancellationToken = default);

        Task<EpisodeProgressResponse> RecordEpisodeProgressAsync(
            string userId,
            int tmdbId,
            int seasonNumber,
            int episodeNumber,
            RecordEpisodeProgressRequest request,
            CancellationToken cancellationToken = default);

        Task<PagedResponse<WatchHistoryItemResponse>> GetHistoryAsync(
            string userId,
            MediaType? mediaType,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<List<WatchHistoryItemResponse>> GetContinueWatchingAsync(
            string userId,
            int limit,
            CancellationToken cancellationToken = default);

        Task<TvHistoryResponse?> GetTvHistoryAsync(
            string userId,
            int tmdbId,
            CancellationToken cancellationToken = default);

        Task RemoveAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default);

        Task ClearAsync(
            string userId,
            CancellationToken cancellationToken = default);
    }
}
