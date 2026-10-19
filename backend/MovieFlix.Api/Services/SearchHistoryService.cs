using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.SearchHistory;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public class SearchHistoryService : ISearchHistoryService
    {
        private readonly MovieFlixDbContext _context;

        public SearchHistoryService(MovieFlixDbContext context)
        {
            _context = context;
        }

        public async Task<SearchHistoryResponse?> RecordAsync(
            string userId, string query)
        {
            var trimmed = query.Trim();

            if (trimmed.Length == 0)
            {
                return null;
            }

            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == userId);

            if (!userExists)
            {
                return null;
            }

            var normalized = trimmed.ToUpperInvariant();

            var existing = await _context.SearchHistoryEntries
                .FirstOrDefaultAsync(e =>
                    e.UserId == userId && e.NormalizedQuery == normalized);

            if (existing is null)
            {
                existing = new SearchHistoryEntry
                {
                    UserId = userId,
                    Query = trimmed,
                    NormalizedQuery = normalized,
                    SearchedAt = DateTime.UtcNow
                };
                _context.SearchHistoryEntries.Add(existing);
            }
            else
            {
                // Refresh casing + timestamp on repeat searches.
                existing.Query = trimmed;
                existing.SearchedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return new SearchHistoryResponse
            {
                Id = existing.Id,
                Query = existing.Query,
                SearchedAt = existing.SearchedAt
            };
        }

        public async Task<List<SearchHistoryResponse>> GetRecentAsync(
            string userId, int limit)
        {
            return await _context.SearchHistoryEntries
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.SearchedAt)
                .Take(limit)
                .Select(e => new SearchHistoryResponse
                {
                    Id = e.Id,
                    Query = e.Query,
                    SearchedAt = e.SearchedAt
                })
                .ToListAsync();
        }

        public async Task<bool> RemoveAsync(string userId, int entryId)
        {
            var entry = await _context.SearchHistoryEntries
                .FirstOrDefaultAsync(e =>
                    e.Id == entryId && e.UserId == userId);

            if (entry is null)
            {
                return false;
            }

            _context.SearchHistoryEntries.Remove(entry);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task ClearAsync(string userId)
        {
            var entries = await _context.SearchHistoryEntries
                .Where(e => e.UserId == userId)
                .ToListAsync();

            if (entries.Count == 0)
            {
                return;
            }

            _context.SearchHistoryEntries.RemoveRange(entries);
            await _context.SaveChangesAsync();
        }
    }
}
