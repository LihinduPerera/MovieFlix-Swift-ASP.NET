using MovieFlix.Api.DTOs.Ratings;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public interface IRatingService
    {
        Task<MediaRatingResponse?> GetMyRatingAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default);

        Task<MediaRatingResponse> RateAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            int rating,
            CancellationToken cancellationToken = default);

        Task RemoveAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default);

        Task<CommunityRatingResponse?> GetCommunityAsync(
            MediaType mediaType,
            int tmdbId,
            string? userId,
            CancellationToken cancellationToken = default);
    }
}
