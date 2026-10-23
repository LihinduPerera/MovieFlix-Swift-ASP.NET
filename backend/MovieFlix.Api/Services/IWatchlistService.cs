using MovieFlix.Api.DTOs.Favorites;

namespace MovieFlix.Api.Services
{
    public interface IWatchlistService
    {
        Task<List<FavoriteMovieResponse>> GetUserWatchlistAsync(string userId);

        Task<FavoriteMovieResponse> AddByTmdbIdAsync(
            string userId, int tmdbId, CancellationToken cancellationToken = default);

        Task<FavoriteMovieResponse?> AddAsync(string userId, int movieId);

        Task<bool> RemoveAsync(string userId, int movieId);
    }
}
