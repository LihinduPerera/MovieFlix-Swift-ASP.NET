using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Preferences;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public class PreferencesService : IPreferencesService
    {
        private readonly MovieFlixDbContext _context;
        private readonly TimeProvider _timeProvider;

        public PreferencesService(
            MovieFlixDbContext context,
            TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<PreferencesResponse?> GetAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            var preferences = await GetOrCreateAsync(userId, cancellationToken);

            return preferences is null ? null : MapToResponse(preferences);
        }

        public async Task<PreferencesResponse?> UpdateAsync(
            string userId,
            UpdatePreferencesRequest request,
            CancellationToken cancellationToken = default)
        {
            var preferences = await GetOrCreateAsync(userId, cancellationToken);

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
            preferences.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

            await _context.SaveChangesAsync(cancellationToken);

            return MapToResponse(preferences);
        }

        private async Task<UserPreferences?> GetOrCreateAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId, cancellationToken);

            if (!userExists)
            {
                return null;
            }

            var preferences = await _context.UserPreferences
                .FirstOrDefaultAsync(
                    item => item.UserId == userId,
                    cancellationToken);

            if (preferences is not null)
            {
                return preferences;
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            preferences = new UserPreferences
            {
                UserId = userId,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.UserPreferences.Add(preferences);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();

                preferences = await _context.UserPreferences
                    .FirstOrDefaultAsync(
                        item => item.UserId == userId,
                        cancellationToken);
            }

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
