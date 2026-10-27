using MovieFlix.Api.DTOs.Preferences;

namespace MovieFlix.Api.Services
{
    public interface IPreferencesService
    {
        Task<PreferencesResponse?> GetAsync(
            string userId,
            CancellationToken cancellationToken = default);

        Task<PreferencesResponse?> UpdateAsync(
            string userId,
            UpdatePreferencesRequest request,
            CancellationToken cancellationToken = default);
    }
}
