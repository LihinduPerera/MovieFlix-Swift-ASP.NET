using MovieFlix.Api.DTOs.Favorites;

namespace MovieFlix.Api.Services
{
    public interface IFavoriteService
    {
        Task<List<FavoriteMovieResponse>> GetUserFavoritesAsync(string userId);

        Task<FavoriteMovieResponse> AddFavoriteByTmdbIdAsync(
            string userId, int tmdbId, CancellationToken cancellationToken = default);

        Task<FavoriteMovieResponse?> AddFavoriteAsync(string userId, int movieId);

        Task<bool> RemoveFavoriteAsync(string userId, int movieId);
    }
}
