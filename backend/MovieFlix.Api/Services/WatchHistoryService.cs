using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.History;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public class WatchHistoryService : IWatchHistoryService
    {
        private readonly MovieFlixDbContext _context;

        public WatchHistoryService(MovieFlixDbContext context)
        {
            _context = context;
        }

        public async Task<WatchHistoryResponse?> RecordProgressAsync(
            string userId, int movieId, int progressSeconds, bool completed)
        {
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == userId);

            if (!userExists)
            {
                return null;
            }

            var movie = await _context.Movies
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == movieId);

            if (movie is null)
            {
                return null;
            }

            var entry = await _context.WatchHistoryEntries
                .FirstOrDefaultAsync(e =>
                    e.UserId == userId && e.MovieId == movieId);

            if (entry is null)
            {
                entry = new WatchHistoryEntry
                {
                    UserId = userId,
                    MovieId = movieId,
                    ProgressSeconds = progressSeconds,
                    Completed = completed,
                    WatchedAt = DateTime.UtcNow
                };
                _context.WatchHistoryEntries.Add(entry);
            }
            else
            {
                entry.ProgressSeconds = progressSeconds;
                entry.Completed = completed;
                entry.WatchedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return new WatchHistoryResponse
            {
                Id = entry.Id,
                MovieId = movie.Id,
                TmdbId = movie.TmdbId,
                Title = movie.Title,
                Overview = movie.Overview,
                WatchedAt = entry.WatchedAt,
                ProgressSeconds = entry.ProgressSeconds,
                Completed = entry.Completed
            };
        }

        public async Task<PagedResponse<WatchHistoryResponse>> GetRecentlyWatchedAsync(
            string userId, int page, int pageSize)
        {
            var query = _context.WatchHistoryEntries
                .AsNoTracking()
                .Where(e => e.UserId == userId);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(e => e.WatchedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToResponse())
                .ToListAsync();

            return new PagedResponse<WatchHistoryResponse>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<List<WatchHistoryResponse>> GetContinueWatchingAsync(
            string userId)
        {
            return await _context.WatchHistoryEntries
                .AsNoTracking()
                .Where(e =>
                    e.UserId == userId
                    && !e.Completed
                    && e.ProgressSeconds > 0)
                .OrderByDescending(e => e.WatchedAt)
                .Select(MapToResponse())
                .ToListAsync();
        }

        public async Task<bool> RemoveAsync(string userId, int movieId)
        {
            var entry = await _context.WatchHistoryEntries
                .FirstOrDefaultAsync(e =>
                    e.UserId == userId && e.MovieId == movieId);

            if (entry is null)
            {
                return false;
            }

            _context.WatchHistoryEntries.Remove(entry);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task ClearAsync(string userId)
        {
            var entries = await _context.WatchHistoryEntries
                .Where(e => e.UserId == userId)
                .ToListAsync();

            if (entries.Count == 0)
            {
                return;
            }

            _context.WatchHistoryEntries.RemoveRange(entries);
            await _context.SaveChangesAsync();
        }

        private static System.Linq.Expressions.Expression<
            Func<WatchHistoryEntry, WatchHistoryResponse>> MapToResponse()
        {
            return e => new WatchHistoryResponse
            {
                Id = e.Id,
                MovieId = e.MovieId,
                TmdbId = e.Movie.TmdbId,
                Title = e.Movie.Title,
                Overview = e.Movie.Overview,
                WatchedAt = e.WatchedAt,
                ProgressSeconds = e.ProgressSeconds,
                Completed = e.Completed
            };
        }
    }
}
