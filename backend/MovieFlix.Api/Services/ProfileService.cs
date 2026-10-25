using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Profile;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public interface IProfileService
    {
        Task<MeResponse?> GetAsync(
            string userId,
            CancellationToken cancellationToken = default);

        Task<MeResponse> UpdateAsync(
            string userId,
            UpdateProfileRequest request,
            CancellationToken cancellationToken = default);

        Task<StatsResponse> GetStatsAsync(
            string userId,
            CancellationToken cancellationToken = default);
    }

    public class ProfileService : IProfileService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly MovieFlixDbContext _context;
        private readonly TimeProvider _timeProvider;

        public ProfileService(
            UserManager<ApplicationUser> userManager,
            MovieFlixDbContext context,
            TimeProvider timeProvider)
        {
            _userManager = userManager;
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<MeResponse?> GetAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = await _userManager.FindByIdAsync(userId);

            if (user is null)
            {
                return null;
            }

            return new MeResponse
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                CreatedAt = user.CreatedAt,
                Roles = (await _userManager.GetRolesAsync(user)).ToList()
            };
        }

        public async Task<MeResponse> UpdateAsync(
            string userId,
            UpdateProfileRequest request,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new ResourceNotFoundException("User not found.");

            user.DisplayName = request.DisplayName.Trim();
            user.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                throw new IdentityValidationException(result.Errors);
            }

            return new MeResponse
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                CreatedAt = user.CreatedAt,
                Roles = (await _userManager.GetRolesAsync(user)).ToList()
            };
        }

        public async Task<StatsResponse> GetStatsAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            return new StatsResponse
            {
                Favorites = await _context.Favorites
                    .AsNoTracking()
                    .CountAsync(item => item.UserId == userId, cancellationToken),
                Watchlist = await _context.WatchlistItems
                    .AsNoTracking()
                    .CountAsync(item => item.UserId == userId, cancellationToken),
                Ratings = await _context.MediaRatings
                    .AsNoTracking()
                    .CountAsync(item => item.UserId == userId, cancellationToken),
                Reviews = await _context.Reviews
                    .AsNoTracking()
                    .CountAsync(item => item.UserId == userId, cancellationToken),
                Collections = await _context.Collections
                    .AsNoTracking()
                    .CountAsync(item => item.UserId == userId, cancellationToken),
                MoviesWatched = await _context.MediaWatchStates
                    .AsNoTracking()
                    .CountAsync(state => state.UserId == userId
                        && state.Completed
                        && state.MediaItem.MediaType == MediaType.Movie,
                        cancellationToken),
                ShowsWatched = await _context.MediaWatchStates
                    .AsNoTracking()
                    .CountAsync(state => state.UserId == userId
                        && state.Completed
                        && state.MediaItem.MediaType == MediaType.Tv,
                        cancellationToken),
                EpisodesWatched = await _context.EpisodeWatchStates
                    .AsNoTracking()
                    .CountAsync(state => state.UserId == userId
                        && state.Completed,
                        cancellationToken)
            };
        }
    }
}
