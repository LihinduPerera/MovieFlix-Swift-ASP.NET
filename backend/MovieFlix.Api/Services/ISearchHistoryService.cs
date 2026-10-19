using MovieFlix.Api.DTOs.SearchHistory;

namespace MovieFlix.Api.Services
{
    public interface ISearchHistoryService
    {
        /// <summary>
        /// Records a search term for the user. Repeat searches of the same term
        /// (case-insensitive) refresh the existing entry rather than duplicate
        /// it. Returns null when the user does not exist.
        /// </summary>
        Task<SearchHistoryResponse?> RecordAsync(string userId, string query);

        /// <summary>Most recent distinct search terms, newest first.</summary>
        Task<List<SearchHistoryResponse>> GetRecentAsync(string userId, int limit);

        /// <summary>Removes a single history entry. Returns false if not found.</summary>
        Task<bool> RemoveAsync(string userId, int entryId);

        /// <summary>Removes all of the user's search history.</summary>
        Task ClearAsync(string userId);
    }
}
