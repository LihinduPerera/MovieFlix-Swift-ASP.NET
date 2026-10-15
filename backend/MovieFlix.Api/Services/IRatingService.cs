using MovieFlix.Api.DTOs.Ratings;

namespace MovieFlix.Api.Services
{
    public interface IRatingService
    {
        /// <summary>
        /// Creates or updates the user's rating for a movie and returns the
        /// updated rating. Returns null when the user or movie does not exist.
        /// </summary>
        Task<UserRatingResponse?> RateAsync(
            string userId, int movieId, int rating);

        /// <summary>Gets the current user's rating for a movie, or null.</summary>
        Task<UserRatingResponse?> GetMyRatingAsync(string userId, int movieId);

        /// <summary>Removes the user's rating. Returns false if none existed.</summary>
        Task<bool> RemoveAsync(string userId, int movieId);

        /// <summary>
        /// Computes the community average for a movie, including the current
        /// user's own rating if they have one.
        /// </summary>
        Task<CommunityRatingResponse?> GetCommunityRatingAsync(
            string? userId, int movieId);
    }
}
