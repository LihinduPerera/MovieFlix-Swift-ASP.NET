using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.History;

namespace MovieFlix.Api.Services
{
    public interface IWatchHistoryService
    {
        /// <summary>
        /// Records (or updates) playback progress for a movie. Returns null
        /// when the user or movie does not exist.
        /// </summary>
        Task<WatchHistoryResponse?> RecordProgressAsync(
            string userId, int movieId, int progressSeconds, bool completed);

        /// <summary>Recently watched movies, newest first, paginated.</summary>
        Task<PagedResponse<WatchHistoryResponse>> GetRecentlyWatchedAsync(
            string userId, int page, int pageSize);

        /// <summary>
        /// Movies that are started but not finished, newest first, for the
        /// "Continue Watching" rail.
        /// </summary>
        Task<List<WatchHistoryResponse>> GetContinueWatchingAsync(string userId);

        Task<bool> RemoveAsync(string userId, int movieId);

        Task ClearAsync(string userId);
    }
}
