using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.DTOs.Reviews;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public interface IReviewService
    {
        Task<PagedResponse<ReviewResponse>?> GetForMediaAsync(
            MediaType mediaType,
            int tmdbId,
            int page,
            int pageSize,
            string? userId,
            CancellationToken cancellationToken = default);

        Task<PagedResponse<ReviewResponse>> GetForUserAsync(
            string userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<ReviewResponse> CreateAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            CreateReviewRequest request,
            CancellationToken cancellationToken = default);

        Task<ReviewResponse> UpdateAsync(
            string userId,
            int reviewId,
            UpdateReviewRequest request,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            string userId,
            int reviewId,
            CancellationToken cancellationToken = default);
    }
}
