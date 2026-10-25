using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public interface IWatchlistService
    {
        Task<PagedResponse<SavedMediaResponse>> GetAsync(
            string userId,
            MediaType? mediaType,
            int page,
            int pageSize,
            string? sort,
            CancellationToken cancellationToken = default);

        Task<SavedMediaResponse> AddAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default);

        Task RemoveAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default);
    }
}
