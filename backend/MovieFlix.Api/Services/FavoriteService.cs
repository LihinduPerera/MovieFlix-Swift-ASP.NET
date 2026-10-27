using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>Favorites are idempotent membership rows keyed by (user, title).</summary>
    public class FavoriteService : IFavoriteService
    {
        private readonly MovieFlixDbContext _context;
        private readonly IMediaCacheService _cacheService;
        private readonly TimeProvider _timeProvider;
        private readonly TmdbSettings _settings;

        public FavoriteService(
            MovieFlixDbContext context,
            IMediaCacheService cacheService,
            TimeProvider timeProvider,
            IOptions<TmdbSettings> settings)
        {
            _context = context;
            _cacheService = cacheService;
            _timeProvider = timeProvider;
            _settings = settings.Value;
        }

        public async Task<PagedResponse<SavedMediaResponse>> GetAsync(
            string userId,
            MediaType? mediaType,
            int page,
            int pageSize,
            string? sort,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var normalizedSort = LibrarySort.Normalize(sort);

            var query = _context.Favorites
                .AsNoTracking()
                .Where(favorite => favorite.UserId == userId);

            if (mediaType is not null)
            {
                query = query.Where(favorite =>
                    favorite.MediaItem.MediaType == mediaType.Value);
            }

            query = normalizedSort switch
            {
                LibrarySort.AddedAsc => query.OrderBy(favorite => favorite.CreatedAt),
                LibrarySort.TitleAsc => query.OrderBy(favorite => favorite.MediaItem.Title),
                LibrarySort.TitleDesc => query.OrderByDescending(favorite => favorite.MediaItem.Title),
                LibrarySort.ReleaseDesc => query
                    .OrderByDescending(favorite => favorite.MediaItem.ReleaseDate)
                    .ThenByDescending(favorite => favorite.CreatedAt),
                _ => query.OrderByDescending(favorite => favorite.CreatedAt)
            };

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(favorite => new SavedMediaResponse
                {
                    Media = new MediaSummaryResponse
                    {
                        TmdbId = favorite.MediaItem.TmdbId,
                        MediaType = favorite.MediaItem.MediaType,
                        Title = favorite.MediaItem.Title,
                        OriginalTitle = favorite.MediaItem.OriginalTitle,
                        Overview = favorite.MediaItem.Overview,
                        PosterPath = favorite.MediaItem.PosterPath,
                        BackdropPath = favorite.MediaItem.BackdropPath,
                        ReleaseDate = favorite.MediaItem.ReleaseDate,
                        VoteAverage = favorite.MediaItem.VoteAverage,
                        VoteCount = favorite.MediaItem.VoteCount,
                        Popularity = favorite.MediaItem.Popularity
                    },
                    SavedAt = favorite.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new PagedResponse<SavedMediaResponse>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<SavedMediaResponse> AddAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            var media = await _cacheService.GetOrCreateAsync(
                mediaType,
                tmdbId,
                _settings.DefaultLanguage,
                cancellationToken);

            var existing = await GetOneAsync(
                userId,
                media.Id,
                cancellationToken);

            if (existing is not null)
            {
                return existing;
            }

            var favorite = new Favorite
            {
                UserId = userId,
                MediaItemId = media.Id,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            _context.Favorites.Add(favorite);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Concurrent add: the unique index won, return the existing row.
                _context.ChangeTracker.Clear();
            }

            return await GetOneAsync(userId, media.Id, cancellationToken)
                ?? throw new InvalidOperationException("Favorite could not be saved.");
        }

        public async Task RemoveAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            var favorite = await _context.Favorites
                .FirstOrDefaultAsync(item => item.UserId == userId
                    && item.MediaItem.MediaType == mediaType
                    && item.MediaItem.TmdbId == tmdbId,
                    cancellationToken);

            if (favorite is null)
            {
                return;
            }

            _context.Favorites.Remove(favorite);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private Task<SavedMediaResponse?> GetOneAsync(
            string userId,
            int mediaItemId,
            CancellationToken cancellationToken)
        {
            return _context.Favorites
                .AsNoTracking()
                .Where(favorite => favorite.UserId == userId
                    && favorite.MediaItemId == mediaItemId)
                .Select(favorite => new SavedMediaResponse
                {
                    Media = new MediaSummaryResponse
                    {
                        TmdbId = favorite.MediaItem.TmdbId,
                        MediaType = favorite.MediaItem.MediaType,
                        Title = favorite.MediaItem.Title,
                        OriginalTitle = favorite.MediaItem.OriginalTitle,
                        Overview = favorite.MediaItem.Overview,
                        PosterPath = favorite.MediaItem.PosterPath,
                        BackdropPath = favorite.MediaItem.BackdropPath,
                        ReleaseDate = favorite.MediaItem.ReleaseDate,
                        VoteAverage = favorite.MediaItem.VoteAverage,
                        VoteCount = favorite.MediaItem.VoteCount,
                        Popularity = favorite.MediaItem.Popularity
                    },
                    SavedAt = favorite.CreatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
