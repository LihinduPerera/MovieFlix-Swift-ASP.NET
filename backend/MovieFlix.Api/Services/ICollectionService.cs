using MovieFlix.Api.DTOs.Collections;

namespace MovieFlix.Api.Services
{
    public interface ICollectionService
    {
        Task<List<CollectionSummaryResponse>> GetUserCollectionsAsync(string userId);

        /// <summary>Returns a collection with its movies; null when not found.</summary>
        Task<CollectionDetailResponse?> GetByIdAsync(int collectionId);

        Task<CollectionSummaryResponse> CreateAsync(
            string userId, string name, string description);

        /// <summary>
        /// Updates a collection. Returns null when not found; throws
        /// <see cref="Exceptions.ForbiddenAccessException"/> when not owned.
        /// </summary>
        Task<CollectionSummaryResponse?> UpdateAsync(
            string userId, int collectionId, string name, string description);

        /// <summary>
        /// Deletes a collection. Returns false when not found; throws
        /// <see cref="Exceptions.ForbiddenAccessException"/> when not owned.
        /// </summary>
        Task<bool> DeleteAsync(string userId, int collectionId);

        /// <summary>
        /// Adds a movie to a collection. Returns the updated detail; null when
        /// the collection or movie does not exist; throws Forbidden when not
        /// owned and DuplicateResource when the movie is already present.
        /// </summary>
        Task<CollectionDetailResponse?> AddMovieAsync(
            string userId, int collectionId, int movieId);

        /// <summary>
        /// Removes a movie from a collection. Returns false when not present;
        /// throws Forbidden when not owned.
        /// </summary>
        Task<bool> RemoveMovieAsync(string userId, int collectionId, int movieId);
    }
}
