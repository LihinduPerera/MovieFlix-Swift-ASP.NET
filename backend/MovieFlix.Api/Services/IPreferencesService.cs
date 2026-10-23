using MovieFlix.Api.DTOs.Preferences;

namespace MovieFlix.Api.Services
{
    public interface IPreferencesService
    {
        /// <summary>
        /// Returns the user's preferences, creating a default row on first
        /// access. Returns null when the user does not exist.
        /// </summary>
        Task<PreferencesResponse?> GetAsync(string userId);

        /// <summary>
        /// Updates the user's preferences (creating defaults first if needed).
        /// Returns null when the user does not exist.
        /// </summary>
        Task<PreferencesResponse?> UpdateAsync(
            string userId, UpdatePreferencesRequest request);
    }
}
