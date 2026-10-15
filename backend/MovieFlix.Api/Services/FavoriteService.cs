using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Favorites;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public class FavoriteService : IFavoriteService
    {
        private readonly MovieFlixDbContext _context;
        private readonly IMovieCacheService _movieCacheService;

        public FavoriteService(
            MovieFlixDbContext context,
            IMovieCacheService movieCacheService)
        {
            _context = context;
            _movieCacheService = movieCacheService;
        }

        public async Task<List<FavoriteMovieResponse>> GetUserFavoritesAsync(string userId)
        {
            var favorites = await _context.Favorites
                .AsNoTracking()
                .Where(favorite => favorite.UserId == userId)
                //Include() — load the related entity , Select() — tell the database exactly what you need
                //We Use Select() because 'I don't need Favorite and Movie entities. I need these five pieces of data.'
                //That's called Projection!!!!
                .Select(favorite => new FavoriteMovieResponse
                {
                    Id = favorite.Id,
                    TmdbId = favorite.Movie.TmdbId,
                    Title = favorite.Movie.Title,
                    Overview = favorite.Movie.Overview,
                    FavoritedAt = favorite.CreatedAt
                })
                .ToListAsync();

            return favorites;
        }

        public async Task<FavoriteMovieResponse?> AddFavoriteAsync(string userId, int movieId)
        {
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId);

            if (!userExists)
            {
                return null;
            }

            var movieExists = await _context.Movies
                .AsNoTracking()
                .AnyAsync(movie => movie.Id == movieId);

            if (!movieExists)
            {
                return null;
            }

            var existingFavorite = await _context.Favorites
                .AsNoTracking()
                .FirstOrDefaultAsync(favorite =>
                    favorite.UserId == userId &&
                    favorite.MovieId == movieId);

            if (existingFavorite is not null)
            {
                throw new FavoriteAlreadyExistsException(userId, movieId);
            }

            var favorite = new Favorite
            {
                UserId = userId,
                MovieId = movieId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Favorites.Add(favorite);

            await _context.SaveChangesAsync();

            return await _context.Favorites
                .AsNoTracking()
                .Where(f => f.Id == favorite.Id)
                .Select(f => new FavoriteMovieResponse
                {
                    Id = f.Id,
                    TmdbId = f.Movie.TmdbId,
                    Title = f.Movie.Title,
                    Overview = f.Movie.Overview,
                    FavoritedAt = f.CreatedAt
                })
                .FirstAsync();
        }

        public async Task<bool> RemoveFavoriteAsync(string userId,int movieId)
        {
            var favorite = await _context.Favorites
                .FirstOrDefaultAsync(favorite =>
                    favorite.UserId == userId &&
                    favorite.MovieId == movieId);

            if (favorite is null)
            {
                return false;
            }

            _context.Favorites.Remove(favorite);

            await _context.SaveChangesAsync();

            return true;
        }


        public async Task<FavoriteMovieResponse> AddFavoriteByTmdbIdAsync(
            string userId,
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            // Resolve (and cache) the movie via the shared cache service, then
            // create the favorite. The movie upsert is idempotent, so there is
            // no need to wrap both steps in a single transaction.
            var movie = await _movieCacheService.GetOrCreateByTmdbIdAsync(
                tmdbId,
                cancellationToken);

            var favorite = await AddFavoriteAsync(userId, movie.Id);

            if (favorite is null)
            {
                throw new InvalidOperationException(
                    "The user or movie could not be found.");
            }

            return favorite;
        }
    }
}
