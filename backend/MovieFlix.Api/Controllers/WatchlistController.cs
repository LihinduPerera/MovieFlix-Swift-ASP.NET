using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.Favorites;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class WatchlistController : ControllerBase
    {
        private readonly IWatchlistService _watchlistService;
        private readonly ICurrentUserService _currentUser;

        public WatchlistController(
            IWatchlistService watchlistService,
            ICurrentUserService currentUser)
        {
            _watchlistService = watchlistService;
            _currentUser = currentUser;
        }

        [Authorize]
        [HttpGet("/api/v1/me/watchlist")]
        public async Task<IActionResult> GetMyWatchlist()
        {
            var items = await _watchlistService
                .GetUserWatchlistAsync(_currentUser.UserId);

            return Ok(items);
        }

        [Authorize]
        [HttpPost("/api/v1/me/watchlist/{movieId:int}")]
        public async Task<IActionResult> Add(int movieId)
        {
            var result = await _watchlistService.AddAsync(_currentUser.UserId, movieId);

            if (result is null)
            {
                return NotFound();
            }

            return CreatedAtAction(nameof(GetMyWatchlist), result);
        }

        [Authorize]
        [HttpPost("/api/v1/me/watchlist/tmdb/{tmdbId:int}")]
        public async Task<ActionResult<FavoriteMovieResponse>> AddTmdb(int tmdbId, CancellationToken cancellationToken)
        {
            if (tmdbId <= 0)
            {
                return BadRequest("TMDB movie ID must be greater than zero.");
            }

            var result = await _watchlistService.AddByTmdbIdAsync(
                _currentUser.UserId,
                tmdbId,
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [HttpDelete("/api/v1/me/watchlist/{movieId:int}")]
        public async Task<IActionResult> Remove(int movieId)
        {
            var removed = await _watchlistService.RemoveAsync(_currentUser.UserId, movieId);

            if (!removed)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
