using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.DTOs.Ratings;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>1-5 star ratings plus community aggregates.</summary>
    public class RatingService : IRatingService
    {
        private readonly MovieFlixDbContext _context;
        private readonly IMediaCacheService _cacheService;
        private readonly TimeProvider _timeProvider;
        private readonly TmdbSettings _settings;

        public RatingService(
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

        public Task<MediaRatingResponse?> GetMyRatingAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            return _context.MediaRatings
                .AsNoTracking()
                .Where(rating => rating.UserId == userId
                    && rating.MediaItem.MediaType == mediaType
                    && rating.MediaItem.TmdbId == tmdbId)
                .Select(rating => new MediaRatingResponse
                {
                    Media = new MediaSummaryResponse
                    {
                        TmdbId = rating.MediaItem.TmdbId,
                        MediaType = rating.MediaItem.MediaType,
                        Title = rating.MediaItem.Title,
                        OriginalTitle = rating.MediaItem.OriginalTitle,
                        Overview = rating.MediaItem.Overview,
                        PosterPath = rating.MediaItem.PosterPath,
                        BackdropPath = rating.MediaItem.BackdropPath,
                        ReleaseDate = rating.MediaItem.ReleaseDate,
                        VoteAverage = rating.MediaItem.VoteAverage,
                        VoteCount = rating.MediaItem.VoteCount,
                        Popularity = rating.MediaItem.Popularity
                    },
                    Rating = rating.Rating,
                    RatedAt = rating.UpdatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<MediaRatingResponse> RateAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            int rating,
            CancellationToken cancellationToken = default)
        {
            if (rating is < 1 or > 5)
            {
                throw new RequestValidationException(
                    "rating",
                    "Rating must be between 1 and 5.");
            }

            var media = await _cacheService.GetOrCreateAsync(
                mediaType,
                tmdbId,
                _settings.DefaultLanguage,
                cancellationToken);

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var entity = await _context.MediaRatings
                .FirstOrDefaultAsync(item => item.UserId == userId
                    && item.MediaItemId == media.Id,
                    cancellationToken);

            if (entity is null)
            {
                entity = new MediaRating
                {
                    UserId = userId,
                    MediaItemId = media.Id,
                    Rating = rating,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.MediaRatings.Add(entity);
            }
            else
            {
                entity.Rating = rating;
                entity.UpdatedAt = now;
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Concurrent first rating: return the winner.
                _context.ChangeTracker.Clear();

                var existing = await GetMyRatingAsync(
                    userId,
                    mediaType,
                    tmdbId,
                    cancellationToken);

                return existing
                    ?? throw new InvalidOperationException("Rating could not be saved.");
            }

            return new MediaRatingResponse
            {
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
                Rating = entity.Rating,
                RatedAt = entity.UpdatedAt
            };
        }

        public async Task RemoveAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            var rating = await _context.MediaRatings
                .FirstOrDefaultAsync(item => item.UserId == userId
                    && item.MediaItem.MediaType == mediaType
                    && item.MediaItem.TmdbId == tmdbId,
                    cancellationToken);

            if (rating is null)
            {
                return;
            }

            _context.MediaRatings.Remove(rating);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<CommunityRatingResponse?> GetCommunityAsync(
            MediaType mediaType,
            int tmdbId,
            string? userId,
            CancellationToken cancellationToken = default)
        {
            var media = await _context.MediaItems
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.MediaType == mediaType && item.TmdbId == tmdbId,
                    cancellationToken);

            if (media is null)
            {
                return null;
            }

            var query = _context.MediaRatings
                .AsNoTracking()
                .Where(rating => rating.MediaItemId == media.Id);

            var ratingCount = await query.CountAsync(cancellationToken);
            var average = ratingCount == 0
                ? 0
                : await query.AverageAsync(
                    rating => (double)rating.Rating,
                    cancellationToken);

            int? userRating = null;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                userRating = await _context.MediaRatings
                    .AsNoTracking()
                    .Where(rating => rating.UserId == userId
                        && rating.MediaItemId == media.Id)
                    .Select(rating => (int?)rating.Rating)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return new CommunityRatingResponse
            {
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
                AverageRating = Math.Round(average, 2),
                RatingCount = ratingCount,
                UserRating = userRating
            };
        }
    }
}
