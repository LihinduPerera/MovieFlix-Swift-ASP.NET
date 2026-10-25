using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.DTOs.Reviews;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Mappings;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>One review per user per title; content is editable by its owner.</summary>
    public class ReviewService : IReviewService
    {
        private readonly MovieFlixDbContext _context;
        private readonly IMediaCacheService _cacheService;
        private readonly TimeProvider _timeProvider;
        private readonly TmdbSettings _settings;

        public ReviewService(
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

        public async Task<PagedResponse<ReviewResponse>?> GetForMediaAsync(
            MediaType mediaType,
            int tmdbId,
            int page,
            int pageSize,
            string? userId,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var media = await _context.MediaItems
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.MediaType == mediaType && item.TmdbId == tmdbId,
                    cancellationToken);

            if (media is null)
            {
                return null;
            }

            var query = _context.Reviews
                .AsNoTracking()
                .Where(review => review.MediaItemId == media.Id);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(review => review.CreatedAt)
                .ThenByDescending(review => review.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(review => new ReviewResponse
                {
                    Id = review.Id,
                    Media = new MediaSummaryResponse
                    {
                        TmdbId = media.TmdbId,
                        MediaType = media.MediaType,
                        Title = media.Title,
                        OriginalTitle = media.OriginalTitle,
                        Overview = media.Overview,
                        PosterPath = media.PosterPath,
                        BackdropPath = media.BackdropPath,
                        ReleaseDate = media.ReleaseDate,
                        VoteAverage = media.VoteAverage,
                        VoteCount = media.VoteCount,
                        Popularity = media.Popularity
                    },
                    Content = review.Content,
                    AuthorDisplayName = review.User.DisplayName,
                    IsMine = userId != null && review.UserId == userId,
                    CreatedAt = review.CreatedAt,
                    UpdatedAt = review.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            return new PagedResponse<ReviewResponse>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<PagedResponse<ReviewResponse>> GetForUserAsync(
            string userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Reviews
                .AsNoTracking()
                .Where(review => review.UserId == userId);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(review => review.UpdatedAt)
                .ThenByDescending(review => review.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(review => new ReviewResponse
                {
                    Id = review.Id,
                    Media = new MediaSummaryResponse
                    {
                        TmdbId = review.MediaItem.TmdbId,
                        MediaType = review.MediaItem.MediaType,
                        Title = review.MediaItem.Title,
                        OriginalTitle = review.MediaItem.OriginalTitle,
                        Overview = review.MediaItem.Overview,
                        PosterPath = review.MediaItem.PosterPath,
                        BackdropPath = review.MediaItem.BackdropPath,
                        ReleaseDate = review.MediaItem.ReleaseDate,
                        VoteAverage = review.MediaItem.VoteAverage,
                        VoteCount = review.MediaItem.VoteCount,
                        Popularity = review.MediaItem.Popularity
                    },
                    Content = review.Content,
                    AuthorDisplayName = review.User.DisplayName,
                    IsMine = true,
                    CreatedAt = review.CreatedAt,
                    UpdatedAt = review.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            return new PagedResponse<ReviewResponse>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<ReviewResponse> CreateAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            CreateReviewRequest request,
            CancellationToken cancellationToken = default)
        {
            var media = await _cacheService.GetOrCreateAsync(
                mediaType,
                tmdbId,
                _settings.DefaultLanguage,
                cancellationToken);

            var duplicate = await _context.Reviews
                .AnyAsync(review => review.UserId == userId
                    && review.MediaItemId == media.Id,
                    cancellationToken);

            if (duplicate)
            {
                throw new ReviewAlreadyExistsException();
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var review = new Review
            {
                UserId = userId,
                MediaItemId = media.Id,
                Content = request.Content.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Reviews.Add(review);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                throw new ReviewAlreadyExistsException();
            }

            var displayName = await _context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.DisplayName)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            return new ReviewResponse
            {
                Id = review.Id,
                Media = new MediaSummaryResponse
                {
                    TmdbId = media.TmdbId,
                    MediaType = media.MediaType,
                    Title = media.Title,
                    OriginalTitle = media.OriginalTitle,
                    Overview = media.Overview,
                    PosterPath = media.PosterPath,
                    BackdropPath = media.BackdropPath,
                    ReleaseDate = media.ReleaseDate,
                    VoteAverage = media.VoteAverage,
                    VoteCount = media.VoteCount,
                    Popularity = media.Popularity
                },
                Content = review.Content,
                AuthorDisplayName = displayName,
                IsMine = true,
                CreatedAt = review.CreatedAt,
                UpdatedAt = review.UpdatedAt
            };
        }

        public async Task<ReviewResponse> UpdateAsync(
            string userId,
            int reviewId,
            UpdateReviewRequest request,
            CancellationToken cancellationToken = default)
        {
            var review = await _context.Reviews
                .Include(item => item.MediaItem)
                .Include(item => item.User)
                .FirstOrDefaultAsync(item => item.Id == reviewId, cancellationToken)
                ?? throw new ResourceNotFoundException("Review not found.");

            if (review.UserId != userId)
            {
                throw new ForbiddenAccessException();
            }

            review.Content = request.Content.Trim();
            review.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

            await _context.SaveChangesAsync(cancellationToken);

            return new ReviewResponse
            {
                Id = review.Id,
                Media = MediaMappings.ToSummary(review.MediaItem),
                Content = review.Content,
                AuthorDisplayName = review.User.DisplayName,
                IsMine = true,
                CreatedAt = review.CreatedAt,
                UpdatedAt = review.UpdatedAt
            };
        }

        public async Task DeleteAsync(
            string userId,
            int reviewId,
            CancellationToken cancellationToken = default)
        {
            var review = await _context.Reviews
                .FirstOrDefaultAsync(item => item.Id == reviewId, cancellationToken)
                ?? throw new ResourceNotFoundException("Review not found.");

            if (review.UserId != userId)
            {
                throw new ForbiddenAccessException();
            }

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
