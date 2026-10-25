using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Collections;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Mappings;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>
    /// Collections are user-owned; names are unique per owner ignoring case.
    /// Cover art is derived from up to four newest posters, never stored.
    /// </summary>
    public class CollectionService : ICollectionService
    {
        private const int CoverLimit = 4;

        private readonly MovieFlixDbContext _context;
        private readonly IMediaCacheService _cacheService;
        private readonly TimeProvider _timeProvider;
        private readonly TmdbSettings _settings;

        public CollectionService(
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

        public async Task<PagedResponse<CollectionSummaryResponse>> GetUserCollectionsAsync(
            string userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Collections
                .AsNoTracking()
                .Where(collection => collection.UserId == userId);

            var totalCount = await query.CountAsync(cancellationToken);

            var collections = await query
                .OrderByDescending(collection => collection.UpdatedAt)
                .ThenByDescending(collection => collection.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(collection => new
                {
                    collection.Id,
                    collection.Name,
                    collection.Description,
                    collection.CreatedAt,
                    collection.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            var ids = collections.Select(collection => collection.Id).ToList();
            var counts = await GetCountsAsync(ids, cancellationToken);
            var covers = await GetCoversAsync(ids, cancellationToken);

            var items = collections
                .Select(collection => new CollectionSummaryResponse
                {
                    Id = collection.Id,
                    Name = collection.Name,
                    Description = collection.Description,
                    ItemCount = counts.GetValueOrDefault(collection.Id),
                    CoverPosterPaths = covers.GetValueOrDefault(collection.Id) ?? [],
                    CreatedAt = collection.CreatedAt,
                    UpdatedAt = collection.UpdatedAt
                })
                .ToList();

            return new PagedResponse<CollectionSummaryResponse>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<CollectionDetailResponse?> GetByIdAsync(
            int collectionId,
            string? userId,
            CancellationToken cancellationToken = default)
        {
            var collection = await _context.Collections
                .AsNoTracking()
                .Include(item => item.User)
                .FirstOrDefaultAsync(item => item.Id == collectionId, cancellationToken);

            if (collection is null)
            {
                return null;
            }

            var items = await _context.CollectionItems
                .AsNoTracking()
                .Where(item => item.CollectionId == collectionId)
                .OrderByDescending(item => item.AddedAt)
                .ThenByDescending(item => item.Id)
                .Select(item => new CollectionItemResponse
                {
                    Media = new DTOs.Media.MediaSummaryResponse
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
                    AddedAt = item.AddedAt
                })
                .ToListAsync(cancellationToken);

            return new CollectionDetailResponse
            {
                Id = collection.Id,
                Name = collection.Name,
                Description = collection.Description,
                OwnerDisplayName = collection.User.DisplayName,
                IsOwner = userId != null && collection.UserId == userId,
                Items = items,
                ItemCount = items.Count,
                CoverPosterPaths = items
                    .Where(item => item.Media.PosterPath is not null)
                    .Take(CoverLimit)
                    .Select(item => item.Media.PosterPath!)
                    .ToList(),
                CreatedAt = collection.CreatedAt,
                UpdatedAt = collection.UpdatedAt
            };
        }

        public async Task<CollectionSummaryResponse> CreateAsync(
            string userId,
            CreateCollectionRequest request,
            CancellationToken cancellationToken = default)
        {
            var name = NormalizeName(request.Name);
            var normalized = name.ToUpperInvariant();
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            await EnsureNameAvailableAsync(
                userId,
                normalized,
                excludedId: null,
                cancellationToken);

            var collection = new Collection
            {
                UserId = userId,
                Name = name,
                NormalizedName = normalized,
                Description = request.Description?.Trim() ?? string.Empty,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Collections.Add(collection);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                throw new DuplicateResourceException(
                    "A collection with that name already exists.");
            }

            return await GetSummaryAsync(collection.Id, cancellationToken)
                ?? throw new InvalidOperationException("Collection could not be saved.");
        }

        public async Task<CollectionSummaryResponse> UpdateAsync(
            string userId,
            int collectionId,
            UpdateCollectionRequest request,
            CancellationToken cancellationToken = default)
        {
            var collection = await GetOwnedAsync(
                userId,
                collectionId,
                includeItems: false,
                cancellationToken);

            var name = NormalizeName(request.Name);
            var normalized = name.ToUpperInvariant();

            await EnsureNameAvailableAsync(
                userId,
                normalized,
                excludedId: collection.Id,
                cancellationToken);

            collection.Name = name;
            collection.NormalizedName = normalized;
            collection.Description = request.Description?.Trim() ?? string.Empty;
            collection.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                throw new DuplicateResourceException(
                    "A collection with that name already exists.");
            }

            return await GetSummaryAsync(collection.Id, cancellationToken)
                ?? throw new InvalidOperationException("Collection could not be saved.");
        }

        public async Task DeleteAsync(
            string userId,
            int collectionId,
            CancellationToken cancellationToken = default)
        {
            var collection = await GetOwnedAsync(
                userId,
                collectionId,
                includeItems: false,
                cancellationToken);

            _context.Collections.Remove(collection);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<CollectionDetailResponse> AddItemAsync(
            string userId,
            int collectionId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            var collection = await GetOwnedAsync(
                userId,
                collectionId,
                includeItems: false,
                cancellationToken);

            var media = await _cacheService.GetOrCreateAsync(
                mediaType,
                tmdbId,
                _settings.DefaultLanguage,
                cancellationToken);

            var exists = await _context.CollectionItems
                .AnyAsync(item => item.CollectionId == collectionId
                    && item.MediaItemId == media.Id,
                    cancellationToken);

            if (!exists)
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;

                _context.CollectionItems.Add(new CollectionItem
                {
                    CollectionId = collectionId,
                    MediaItemId = media.Id,
                    AddedAt = now
                });

                collection.UpdatedAt = now;

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    _context.ChangeTracker.Clear();
                }
            }

            return await GetByIdAsync(collectionId, userId, cancellationToken)
                ?? throw new InvalidOperationException("Collection could not be loaded.");
        }

        public async Task RemoveItemAsync(
            string userId,
            int collectionId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            var collection = await GetOwnedAsync(
                userId,
                collectionId,
                includeItems: false,
                cancellationToken);

            var item = await _context.CollectionItems
                .FirstOrDefaultAsync(entry => entry.CollectionId == collectionId
                    && entry.MediaItem.MediaType == mediaType
                    && entry.MediaItem.TmdbId == tmdbId,
                    cancellationToken);

            if (item is null)
            {
                return;
            }

            _context.CollectionItems.Remove(item);
            collection.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<Collection> GetOwnedAsync(
            string userId,
            int collectionId,
            bool includeItems,
            CancellationToken cancellationToken)
        {
            IQueryable<Collection> query = _context.Collections;

            if (includeItems)
            {
                query = query.Include(collection => collection.Items);
            }

            var collection = await query.FirstOrDefaultAsync(
                item => item.Id == collectionId,
                cancellationToken)
                ?? throw new ResourceNotFoundException("Collection not found.");

            if (collection.UserId != userId)
            {
                throw new ForbiddenAccessException();
            }

            return collection;
        }

        private async Task<CollectionSummaryResponse?> GetSummaryAsync(
            int collectionId,
            CancellationToken cancellationToken)
        {
            var collection = await _context.Collections
                .AsNoTracking()
                .Where(item => item.Id == collectionId)
                .Select(item => new
                {
                    item.Id,
                    item.Name,
                    item.Description,
                    item.CreatedAt,
                    item.UpdatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (collection is null)
            {
                return null;
            }

            var counts = await GetCountsAsync([collection.Id], cancellationToken);
            var covers = await GetCoversAsync([collection.Id], cancellationToken);

            return new CollectionSummaryResponse
            {
                Id = collection.Id,
                Name = collection.Name,
                Description = collection.Description,
                ItemCount = counts.GetValueOrDefault(collection.Id),
                CoverPosterPaths = covers.GetValueOrDefault(collection.Id) ?? [],
                CreatedAt = collection.CreatedAt,
                UpdatedAt = collection.UpdatedAt
            };
        }

        private async Task<Dictionary<int, int>> GetCountsAsync(
            IReadOnlyCollection<int> collectionIds,
            CancellationToken cancellationToken)
        {
            if (collectionIds.Count == 0)
            {
                return [];
            }

            return await _context.CollectionItems
                .AsNoTracking()
                .Where(item => collectionIds.Contains(item.CollectionId))
                .GroupBy(item => item.CollectionId)
                .Select(group => new
                {
                    CollectionId = group.Key,
                    Count = group.Count()
                })
                .ToDictionaryAsync(
                    item => item.CollectionId,
                    item => item.Count,
                    cancellationToken);
        }

        private async Task<Dictionary<int, List<string>>> GetCoversAsync(
            IReadOnlyCollection<int> collectionIds,
            CancellationToken cancellationToken)
        {
            if (collectionIds.Count == 0)
            {
                return [];
            }

            var rows = await _context.CollectionItems
                .AsNoTracking()
                .Where(item => collectionIds.Contains(item.CollectionId)
                    && item.MediaItem.PosterPath != null)
                .OrderByDescending(item => item.AddedAt)
                .ThenByDescending(item => item.Id)
                .Select(item => new
                {
                    item.CollectionId,
                    item.MediaItem.PosterPath
                })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(row => row.CollectionId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(row => row.PosterPath!)
                        .Take(CoverLimit)
                        .ToList());
        }

        private async Task EnsureNameAvailableAsync(
            string userId,
            string normalizedName,
            int? excludedId,
            CancellationToken cancellationToken)
        {
            var exists = await _context.Collections
                .AsNoTracking()
                .AnyAsync(collection => collection.UserId == userId
                    && collection.NormalizedName == normalizedName
                    && (excludedId == null || collection.Id != excludedId),
                    cancellationToken);

            if (exists)
            {
                throw new DuplicateResourceException(
                    "A collection with that name already exists.");
            }
        }

        private static string NormalizeName(string name)
        {
            var trimmed = name.Trim();

            if (trimmed.Length == 0)
            {
                throw new RequestValidationException(
                    "name",
                    "Collection name is required.");
            }

            return trimmed;
        }
    }
}
