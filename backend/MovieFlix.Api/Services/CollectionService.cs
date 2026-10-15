using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Collections;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public class CollectionService : ICollectionService
    {
        private readonly MovieFlixDbContext _context;

        public CollectionService(MovieFlixDbContext context)
        {
            _context = context;
        }

        public async Task<List<CollectionSummaryResponse>> GetUserCollectionsAsync(
            string userId)
        {
            return await _context.Collections
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CollectionSummaryResponse
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    MovieCount = c.Movies.Count,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<CollectionDetailResponse?> GetByIdAsync(int collectionId)
        {
            return await _context.Collections
                .AsNoTracking()
                .Where(c => c.Id == collectionId)
                .Select(c => new CollectionDetailResponse
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    OwnerId = c.UserId,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    Movies = c.Movies
                        .OrderByDescending(cm => cm.AddedAt)
                        .Select(cm => new CollectionMovieResponse
                        {
                            MovieId = cm.MovieId,
                            TmdbId = cm.Movie.TmdbId,
                            Title = cm.Movie.Title,
                            Overview = cm.Movie.Overview,
                            AddedAt = cm.AddedAt
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();
        }

        public async Task<CollectionSummaryResponse> CreateAsync(
            string userId, string name, string description)
        {
            var collection = new Collection
            {
                UserId = userId,
                Name = name.Trim(),
                Description = description?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Collections.Add(collection);
            await _context.SaveChangesAsync();

            return new CollectionSummaryResponse
            {
                Id = collection.Id,
                Name = collection.Name,
                Description = collection.Description,
                MovieCount = 0,
                CreatedAt = collection.CreatedAt,
                UpdatedAt = collection.UpdatedAt
            };
        }

        public async Task<CollectionSummaryResponse?> UpdateAsync(
            string userId, int collectionId, string name, string description)
        {
            var collection = await GetOwnedCollectionAsync(
                userId, collectionId, requireOwnership: true);

            if (collection is null)
            {
                return null;
            }

            collection.Name = name.Trim();
            collection.Description = description?.Trim() ?? string.Empty;
            collection.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var movieCount = await _context.CollectionMovies
                .CountAsync(cm => cm.CollectionId == collection.Id);

            return new CollectionSummaryResponse
            {
                Id = collection.Id,
                Name = collection.Name,
                Description = collection.Description,
                MovieCount = movieCount,
                CreatedAt = collection.CreatedAt,
                UpdatedAt = collection.UpdatedAt
            };
        }

        public async Task<bool> DeleteAsync(string userId, int collectionId)
        {
            var collection = await GetOwnedCollectionAsync(
                userId, collectionId, requireOwnership: true);

            if (collection is null)
            {
                return false;
            }

            _context.Collections.Remove(collection);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<CollectionDetailResponse?> AddMovieAsync(
            string userId, int collectionId, int movieId)
        {
            var collection = await GetOwnedCollectionAsync(
                userId, collectionId, requireOwnership: true);

            if (collection is null)
            {
                return null;
            }

            var movieExists = await _context.Movies
                .AsNoTracking()
                .AnyAsync(m => m.Id == movieId);

            if (!movieExists)
            {
                return null;
            }

            var alreadyAdded = await _context.CollectionMovies
                .AsNoTracking()
                .AnyAsync(cm =>
                    cm.CollectionId == collectionId && cm.MovieId == movieId);

            if (alreadyAdded)
            {
                throw new DuplicateResourceException(
                    $"Movie {movieId} is already in collection {collectionId}.");
            }

            _context.CollectionMovies.Add(new CollectionMovie
            {
                CollectionId = collectionId,
                MovieId = movieId,
                AddedAt = DateTime.UtcNow
            });

            collection.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return await GetByIdAsync(collectionId);
        }

        public async Task<bool> RemoveMovieAsync(
            string userId, int collectionId, int movieId)
        {
            var collection = await GetOwnedCollectionAsync(
                userId, collectionId, requireOwnership: true);

            if (collection is null)
            {
                return false;
            }

            var link = await _context.CollectionMovies
                .FirstOrDefaultAsync(cm =>
                    cm.CollectionId == collectionId && cm.MovieId == movieId);

            if (link is null)
            {
                return false;
            }

            _context.CollectionMovies.Remove(link);
            collection.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Loads a collection, enforcing ownership when requested. Throws
        /// <see cref="ForbiddenAccessException"/> if the collection exists but
        /// belongs to another user.
        /// </summary>
        private async Task<Collection?> GetOwnedCollectionAsync(
            string userId, int collectionId, bool requireOwnership)
        {
            var collection = await _context.Collections
                .FirstOrDefaultAsync(c => c.Id == collectionId);

            if (collection is null)
            {
                return null;
            }

            if (requireOwnership && collection.UserId != userId)
            {
                throw new ForbiddenAccessException(
                    "You can only modify your own collections.");
            }

            return collection;
        }
    }
}
