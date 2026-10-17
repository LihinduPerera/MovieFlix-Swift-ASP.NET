using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Ratings;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public class RatingService : IRatingService
    {
        private readonly MovieFlixDbContext _context;

        public RatingService(MovieFlixDbContext context)
        {
            _context = context;
        }

        public async Task<UserRatingResponse?> RateAsync(
            string userId, int movieId, int rating)
        {
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == userId);

            var movie = await _context.Movies
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == movieId);

            if (!userExists || movie is null)
            {
                return null;
            }

            var existing = await _context.MovieRatings
                .FirstOrDefaultAsync(r =>
                    r.UserId == userId && r.MovieId == movieId);

            if (existing is null)
            {
                existing = new MovieRating
                {
                    UserId = userId,
                    MovieId = movieId,
                    Rating = rating,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.MovieRatings.Add(existing);
            }
            else
            {
                existing.Rating = rating;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return new UserRatingResponse
            {
                MovieId = movie.Id,
                TmdbId = movie.TmdbId,
                Title = movie.Title,
                Rating = existing.Rating,
                RatedAt = existing.UpdatedAt
            };
        }

        public async Task<UserRatingResponse?> GetMyRatingAsync(
            string userId, int movieId)
        {
            var result = await _context.MovieRatings
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.MovieId == movieId)
                .Select(r => new UserRatingResponse
                {
                    MovieId = r.MovieId,
                    TmdbId = r.Movie.TmdbId,
                    Title = r.Movie.Title,
                    Rating = r.Rating,
                    RatedAt = r.UpdatedAt
                })
                .FirstOrDefaultAsync();

            return result;
        }

        public async Task<bool> RemoveAsync(string userId, int movieId)
        {
            var rating = await _context.MovieRatings
                .FirstOrDefaultAsync(r =>
                    r.UserId == userId && r.MovieId == movieId);

            if (rating is null)
            {
                return false;
            }

            _context.MovieRatings.Remove(rating);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<CommunityRatingResponse?> GetCommunityRatingAsync(
            string? userId, int movieId)
        {
            var movie = await _context.Movies
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == movieId);

            if (movie is null)
            {
                return null;
            }

            // Aggregate in the database: average + count in a single pass.
            var stats = await _context.MovieRatings
                .Where(r => r.MovieId == movieId)
                .GroupBy(r => r.MovieId)
                .Select(g => new
                {
                    Average = Math.Round(g.Average(r => r.Rating), 2),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync();

            var average = stats?.Average ?? 0.0;
            var count = stats?.Count ?? 0;

            int? userRating = null;
            if (!string.IsNullOrEmpty(userId))
            {
                userRating = await _context.MovieRatings
                    .AsNoTracking()
                    .Where(r => r.UserId == userId && r.MovieId == movieId)
                    .Select(r => (int?)r.Rating)
                    .FirstOrDefaultAsync();
            }

            return new CommunityRatingResponse
            {
                TmdbId = movie.TmdbId,
                Title = movie.Title,
                AverageRating = average,
                RatingCount = count,
                UserRating = userRating
            };
        }
    }
}
