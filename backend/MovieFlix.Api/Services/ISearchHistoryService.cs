using MovieFlix.Api.DTOs.SearchHistory;

namespace MovieFlix.Api.Services
{
    public interface ISearchHistoryService
    {
        Task<SearchHistoryResponse?> RecordAsync(
            string userId,
            string query,
            CancellationToken cancellationToken = default);

        Task<List<SearchHistoryResponse>> GetRecentAsync(
            string userId,
            int limit,
            CancellationToken cancellationToken = default);

        Task<bool> RemoveAsync(
            string userId,
            int entryId,
            CancellationToken cancellationToken = default);

        Task ClearAsync(
            string userId,
            CancellationToken cancellationToken = default);
    }
}
