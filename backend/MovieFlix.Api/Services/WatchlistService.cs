using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Favorites;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public class WatchlistService : IWatchlistService
    {
        private readonly MovieFlixDbContext _context;
        private readonly IMovieCacheService _movieCacheService;

        public WatchlistService(
            MovieFlixDbContext context,
            IMovieCacheService movieCacheService)
        {
            _context = context;
            _movieCacheService = movieCacheService;
        }

        public async Task<List<FavoriteMovieResponse>> GetUserWatchlistAsync(string userId)
        {
            var items = await _context.WatchlistItems
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .Select(item => new FavoriteMovieResponse
                {
                    Id = item.Id,
                    TmdbId = item.Movie.TmdbId,
                    Title = item.Movie.Title,
                    Overview = item.Movie.Overview,
                    FavoritedAt = item.AddedAt
                })
                .ToListAsync();

            return items;
        }

        public async Task<FavoriteMovieResponse?> AddAsync(string userId, int movieId)
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

            var existing = await _context.WatchlistItems
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.UserId == userId &&
                    item.MovieId == movieId);

            if (existing is not null)
            {
                throw new FavoriteAlreadyExistsException(userId, movieId);
            }

            var item = new WatchlistItem
            {
                UserId = userId,
                MovieId = movieId,
                AddedAt = DateTime.UtcNow
            };

            _context.WatchlistItems.Add(item);
            await _context.SaveChangesAsync();

            return await _context.WatchlistItems
                .AsNoTracking()
                .Where(i => i.Id == item.Id)
                .Select(i => new FavoriteMovieResponse
                {
                    Id = i.Id,
                    TmdbId = i.Movie.TmdbId,
                    Title = i.Movie.Title,
                    Overview = i.Movie.Overview,
                    FavoritedAt = i.AddedAt
                })
                .FirstAsync();
        }

        public async Task<bool> RemoveAsync(string userId, int movieId)
        {
            var item = await _context.WatchlistItems
                .FirstOrDefaultAsync(item =>
                    item.UserId == userId &&
                    item.MovieId == movieId);

            if (item is null)
            {
                return false;
            }

            _context.WatchlistItems.Remove(item);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<FavoriteMovieResponse> AddByTmdbIdAsync(
            string userId,
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            // Resolve (and cache) the movie via the shared cache service, then
            // create the watchlist item. The movie upsert is idempotent, so the
            // two steps do not need a single shared transaction.
            var movie = await _movieCacheService.GetOrCreateByTmdbIdAsync(
                tmdbId,
                cancellationToken);

            var result = await AddAsync(userId, movie.Id);
            if (result is null)
            {
                throw new InvalidOperationException(
                    "The user or movie could not be found.");
            }

            return result;
        }
    }
}
