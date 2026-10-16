using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Favorites;
using MovieFlix.Api.DTOs.Tmdb;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public class FavoriteService : IFavoriteService
    {
        private readonly MovieFlixDbContext _context;
        private readonly ITmdbService _tmdbService;

        public FavoriteService(MovieFlixDbContext context, ITmdbService tmdbService)
        {
            _context = context;
            _tmdbService = tmdbService;
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
            // Step 1: Check whether the movie already exists locally.
            var movie = await _context.Movies
                .SingleOrDefaultAsync(
                    movie => movie.TmdbId == tmdbId,
                    cancellationToken);

            // Step 2: If missing, fetch its details from TMDB BEFORE
            // opening a database transaction.
            TmdbMovieDetailsResponse? tmdbMovie = null;

            if (movie is null)
            {
                tmdbMovie = await _tmdbService.GetMovieDetailsAsync(
                    tmdbId,
                    cancellationToken);
            }

            // Step 3: Begin a database transaction.
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    cancellationToken);

            // Step 4: Check again inside the transaction.
            // Another request might have inserted the movie while
            // we were fetching details from TMDB.
            movie = await _context.Movies
                .SingleOrDefaultAsync(
                    movie => movie.TmdbId == tmdbId,
                    cancellationToken);

            // Step 5: Create the local movie only if it is still missing.
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

            // Step 6: Create the favorite using existing logic.
            var favorite = await AddFavoriteAsync(userId, movie.Id);

            if (favorite is null)
            {
                throw new InvalidOperationException(
                    "The user or movie could not be found.");
            }

            // Step 7: Commit only after both operations succeed.
            await transaction.CommitAsync(cancellationToken);

            return favorite;
        }
    }
}
