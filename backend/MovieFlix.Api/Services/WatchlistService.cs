using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>Watchlist entries are idempotent membership rows keyed by (user, title).</summary>
    public class WatchlistService : IWatchlistService
    {
        private readonly MovieFlixDbContext _context;
        private readonly IMediaCacheService _cacheService;
        private readonly TimeProvider _timeProvider;
        private readonly TmdbSettings _settings;

        public WatchlistService(
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

            var query = _context.WatchlistItems
                .AsNoTracking()
                .Where(item => item.UserId == userId);

            if (mediaType is not null)
            {
                query = query.Where(item =>
                    item.MediaItem.MediaType == mediaType.Value);
            }

            query = normalizedSort switch
            {
                LibrarySort.AddedAsc => query.OrderBy(item => item.AddedAt),
                LibrarySort.TitleAsc => query.OrderBy(item => item.MediaItem.Title),
                LibrarySort.TitleDesc => query.OrderByDescending(item => item.MediaItem.Title),
                LibrarySort.ReleaseDesc => query
                    .OrderByDescending(item => item.MediaItem.ReleaseDate)
                    .ThenByDescending(item => item.AddedAt),
                _ => query.OrderByDescending(item => item.AddedAt)
            };

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(item => new SavedMediaResponse
                {
                    Media = new MediaSummaryResponse
                    {
                        TmdbId = item.MediaItem.TmdbId,
                        MediaType = item.MediaItem.MediaType,
                        Title = item.MediaItem.Title,
                        OriginalTitle = item.MediaItem.OriginalTitle,
                        Overview = item.MediaItem.Overview,
                        PosterPath = item.MediaItem.PosterPath,
                        BackdropPath = item.MediaItem.BackdropPath,
                        ReleaseDate = item.MediaItem.ReleaseDate,
                        VoteAverage = item.MediaItem.VoteAverage,
                        VoteCount = item.MediaItem.VoteCount,
                        Popularity = item.MediaItem.Popularity
                    },
                    SavedAt = item.AddedAt
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

            var existing = await GetOneAsync(userId, media.Id, cancellationToken);

            if (existing is not null)
            {
                return existing;
            }

            var item = new WatchlistItem
            {
                UserId = userId,
                MediaItemId = media.Id,
                AddedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            _context.WatchlistItems.Add(item);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
            }

            return await GetOneAsync(userId, media.Id, cancellationToken)
                ?? throw new InvalidOperationException("Watchlist item could not be saved.");
        }

        public async Task RemoveAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            var item = await _context.WatchlistItems
                .FirstOrDefaultAsync(entry => entry.UserId == userId
                    && entry.MediaItem.MediaType == mediaType
                    && entry.MediaItem.TmdbId == tmdbId,
                    cancellationToken);

            if (item is null)
            {
                return;
            }

            _context.WatchlistItems.Remove(item);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private Task<SavedMediaResponse?> GetOneAsync(
            string userId,
            int mediaItemId,
            CancellationToken cancellationToken)
        {
            return _context.WatchlistItems
                .AsNoTracking()
                .Where(item => item.UserId == userId
                    && item.MediaItemId == mediaItemId)
                .Select(item => new SavedMediaResponse
                {
                    Media = new MediaSummaryResponse
                    {
                        TmdbId = item.MediaItem.TmdbId,
                        MediaType = item.MediaItem.MediaType,
                        Title = item.MediaItem.Title,
                        OriginalTitle = item.MediaItem.OriginalTitle,
                        Overview = item.MediaItem.Overview,
                        PosterPath = item.MediaItem.PosterPath,
                        BackdropPath = item.MediaItem.BackdropPath,
                        ReleaseDate = item.MediaItem.ReleaseDate,
                        VoteAverage = item.MediaItem.VoteAverage,
                        VoteCount = item.MediaItem.VoteCount,
                        Popularity = item.MediaItem.Popularity
                    },
                    SavedAt = item.AddedAt
                })
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
