using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Reviews;

namespace MovieFlix.Api.Services
{
    public interface IReviewService
    {
        /// <summary>
        /// Creates a review for a movie. Returns null when the movie does not
        /// exist; throws <see cref="Exceptions.ReviewAlreadyExistsException"/>
        /// when the user already reviewed this movie.
        /// </summary>
        Task<ReviewResponse?> CreateAsync(
            string userId, int movieId, string content);

        /// <summary>Paginated reviews for a movie, newest first.</summary>
        Task<PagedResponse<ReviewResponse>?> GetForMovieAsync(
            int movieId, int page, int pageSize);

        /// <summary>All of a user's reviews, newest first.</summary>
        Task<List<ReviewResponse>> GetForUserAsync(string userId);

        /// <summary>
        /// Updates a review. Returns null when it does not exist; throws
        /// <see cref="Exceptions.ForbiddenAccessException"/> when the caller
        /// does not own it.
        /// </summary>
        Task<ReviewResponse?> UpdateAsync(
            string userId, int reviewId, string content);

        /// <summary>
        /// Deletes a review. Returns false when it does not exist; throws
        /// <see cref="Exceptions.ForbiddenAccessException"/> when not owned.
        /// </summary>
        Task<bool> DeleteAsync(string userId, int reviewId);
    }
}
