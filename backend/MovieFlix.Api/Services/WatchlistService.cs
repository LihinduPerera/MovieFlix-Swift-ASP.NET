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
        private readonly ITmdbService _tmdbService;

        public WatchlistService(MovieFlixDbContext context, ITmdbService tmdbService)
        {
            _context = context;
            _tmdbService = tmdbService;
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
            var movie = await _context.Movies
                .SingleOrDefaultAsync(
                    movie => movie.TmdbId == tmdbId,
                    cancellationToken);

            DTOs.Tmdb.TmdbMovieDetailsResponse? tmdbMovie = null;
            if (movie is null)
            {
                tmdbMovie = await _tmdbService.GetMovieDetailsAsync(
                    tmdbId,
                    cancellationToken);
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    cancellationToken);

            movie = await _context.Movies
                .SingleOrDefaultAsync(
                    movie => movie.TmdbId == tmdbId,
                    cancellationToken);

            if (movie is null)
            {
                movie = new Movie
                {
                    TmdbId = tmdbMovie!.Id,
                    Title = tmdbMovie.Title,
                    Overview = tmdbMovie.Overview
                };

                _context.Movies.Add(movie);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var result = await AddAsync(userId, movie.Id);
            if (result is null)
            {
                throw new InvalidOperationException(
                    "The user or movie could not be found.");
            }

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
    }
}
