using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.SearchHistory;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public class SearchHistoryService : ISearchHistoryService
    {
        private readonly MovieFlixDbContext _context;
        private readonly TimeProvider _timeProvider;

        public SearchHistoryService(
            MovieFlixDbContext context,
            TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<SearchHistoryResponse?> RecordAsync(
            string userId,
            string query,
            CancellationToken cancellationToken = default)
        {
            var trimmed = query.Trim();

            if (trimmed.Length == 0)
            {
                return null;
            }

            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId, cancellationToken);

            if (!userExists)
            {
                return null;
            }

            var normalized = trimmed.ToUpperInvariant();
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var existing = await _context.SearchHistoryEntries
                .FirstOrDefaultAsync(
                    entry => entry.UserId == userId
                        && entry.NormalizedQuery == normalized,
                    cancellationToken);

            if (existing is null)
            {
                existing = new SearchHistoryEntry
                {
                    UserId = userId,
                    Query = trimmed,
                    NormalizedQuery = normalized,
                    SearchedAt = now
                };

                _context.SearchHistoryEntries.Add(existing);
            }
            else
            {
                existing.Query = trimmed;
                existing.SearchedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return new SearchHistoryResponse
            {
                Id = existing.Id,
                Query = existing.Query,
                SearchedAt = existing.SearchedAt
            };
        }

        public Task<List<SearchHistoryResponse>> GetRecentAsync(
            string userId,
            int limit,
            CancellationToken cancellationToken = default)
        {
            limit = Math.Clamp(limit, 1, 50);

            return _context.SearchHistoryEntries
                .AsNoTracking()
                .Where(entry => entry.UserId == userId)
                .OrderByDescending(entry => entry.SearchedAt)
                .Take(limit)
                .Select(entry => new SearchHistoryResponse
                {
                    Id = entry.Id,
                    Query = entry.Query,
                    SearchedAt = entry.SearchedAt
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> RemoveAsync(
            string userId,
            int entryId,
            CancellationToken cancellationToken = default)
        {
            var entry = await _context.SearchHistoryEntries
                .FirstOrDefaultAsync(
                    item => item.Id == entryId && item.UserId == userId,
                    cancellationToken);

            if (entry is null)
            {
                return false;
            }

            _context.SearchHistoryEntries.Remove(entry);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task ClearAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            await _context.SearchHistoryEntries
                .Where(entry => entry.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
