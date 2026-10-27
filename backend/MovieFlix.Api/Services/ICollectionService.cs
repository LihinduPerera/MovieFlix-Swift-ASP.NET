using MovieFlix.Api.DTOs.Collections;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public interface ICollectionService
    {
        Task<PagedResponse<CollectionSummaryResponse>> GetUserCollectionsAsync(
            string userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<CollectionDetailResponse?> GetByIdAsync(
            int collectionId,
            string? userId,
            CancellationToken cancellationToken = default);

        Task<CollectionSummaryResponse> CreateAsync(
            string userId,
            CreateCollectionRequest request,
            CancellationToken cancellationToken = default);

        Task<CollectionSummaryResponse> UpdateAsync(
            string userId,
            int collectionId,
            UpdateCollectionRequest request,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            string userId,
            int collectionId,
            CancellationToken cancellationToken = default);

        Task<CollectionDetailResponse> AddItemAsync(
            string userId,
            int collectionId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default);

        Task RemoveItemAsync(
            string userId,
            int collectionId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default);
    }
}
