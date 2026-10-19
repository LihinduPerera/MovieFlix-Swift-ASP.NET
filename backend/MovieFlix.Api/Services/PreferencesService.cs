using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Preferences;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public class PreferencesService : IPreferencesService
    {
        private readonly MovieFlixDbContext _context;

        public PreferencesService(MovieFlixDbContext context)
        {
            _context = context;
        }

        public async Task<PreferencesResponse?> GetAsync(string userId)
        {
            var preferences = await GetOrCreateAsync(userId);

            if (preferences is null)
            {
                return null;
            }

            return MapToResponse(preferences);
        }

        public async Task<PreferencesResponse?> UpdateAsync(
            string userId, UpdatePreferencesRequest request)
        {
            var preferences = await GetOrCreateAsync(userId);

            if (preferences is null)
            {
                return null;
            }

            preferences.PreferredLanguage = request.PreferredLanguage;
            preferences.PreferredRegion = request.PreferredRegion;
            preferences.Theme = request.Theme;
            preferences.AutoPlayTrailers = request.AutoPlayTrailers;
            preferences.EmailNotifications = request.EmailNotifications;
            preferences.MatureContentFilter = request.MatureContentFilter;
            preferences.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MapToResponse(preferences);
        }

        /// <summary>
        /// Loads the user's preferences, inserting a default row the first time
        /// they are accessed. Returns null when the user does not exist.
        /// </summary>
        private async Task<UserPreferences?> GetOrCreateAsync(string userId)
        {
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == userId);

            if (!userExists)
            {
                return null;
            }

            var preferences = await _context.UserPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (preferences is not null)
            {
                return preferences;
            }

            preferences = new UserPreferences
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserPreferences.Add(preferences);
            await _context.SaveChangesAsync();

            return preferences;
        }

        private static PreferencesResponse MapToResponse(UserPreferences preferences)
        {
            return new PreferencesResponse
            {
                PreferredLanguage = preferences.PreferredLanguage,
                PreferredRegion = preferences.PreferredRegion,
                Theme = preferences.Theme,
                AutoPlayTrailers = preferences.AutoPlayTrailers,
                EmailNotifications = preferences.EmailNotifications,
                MatureContentFilter = preferences.MatureContentFilter,
                UpdatedAt = preferences.UpdatedAt
            };
        }
    }
}
