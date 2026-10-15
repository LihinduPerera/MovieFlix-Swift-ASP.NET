using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Reviews;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public class ReviewService : IReviewService
    {
        private readonly MovieFlixDbContext _context;

        public ReviewService(MovieFlixDbContext context)
        {
            _context = context;
        }

        public async Task<ReviewResponse?> CreateAsync(
            string userId, int movieId, string content)
        {
            var movieExists = await _context.Movies
                .AsNoTracking()
                .AnyAsync(m => m.Id == movieId);

            if (!movieExists)
            {
                return null;
            }

            var alreadyReviewed = await _context.Reviews
                .AsNoTracking()
                .AnyAsync(r => r.UserId == userId && r.MovieId == movieId);

            if (alreadyReviewed)
            {
                throw new ReviewAlreadyExistsException(userId, movieId);
            }

            var review = new Review
            {
                UserId = userId,
                MovieId = movieId,
                Content = content.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return await GetByIdAsync(review.Id);
        }

        public async Task<PagedResponse<ReviewResponse>?> GetForMovieAsync(
            int movieId, int page, int pageSize)
        {
            var movieExists = await _context.Movies
                .AsNoTracking()
                .AnyAsync(m => m.Id == movieId);

            if (!movieExists)
            {
                return null;
            }

            var query = _context.Reviews
                .AsNoTracking()
                .Where(r => r.MovieId == movieId);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(Projection())
                .ToListAsync();

            return new PagedResponse<ReviewResponse>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<List<ReviewResponse>> GetForUserAsync(string userId)
        {
            return await _context.Reviews
                .AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(Projection())
                .ToListAsync();
        }

        public async Task<ReviewResponse?> UpdateAsync(
            string userId, int reviewId, string content)
        {
            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == reviewId);

            if (review is null)
            {
                return null;
            }

            if (review.UserId != userId)
            {
                throw new ForbiddenAccessException(
                    "You can only edit your own reviews.");
            }

            review.Content = content.Trim();
            review.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return await GetByIdAsync(review.Id);
        }

        public async Task<bool> DeleteAsync(string userId, int reviewId)
        {
            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == reviewId);

            if (review is null)
            {
                return false;
            }

            if (review.UserId != userId)
            {
                throw new ForbiddenAccessException(
                    "You can only delete your own reviews.");
            }

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<ReviewResponse?> GetByIdAsync(int reviewId)
        {
            return await _context.Reviews
                .AsNoTracking()
                .Where(r => r.Id == reviewId)
                .Select(Projection())
                .FirstOrDefaultAsync();
        }

        private static System.Linq.Expressions.Expression<
            Func<Review, ReviewResponse>> Projection()
        {
            return r => new ReviewResponse
            {
                Id = r.Id,
                MovieId = r.MovieId,
                TmdbId = r.Movie.TmdbId,
                MovieTitle = r.Movie.Title,
                Content = r.Content,
                AuthorId = r.UserId,
                AuthorDisplayName = r.User.DisplayName,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            };
        }
    }
}
