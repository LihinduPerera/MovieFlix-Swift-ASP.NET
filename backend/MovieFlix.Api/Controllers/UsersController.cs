using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.Favorites;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IFavoriteService _favoriteService;
        private readonly ICurrentUserService _currentUser;

        public UsersController(IFavoriteService favoriteService,
            ICurrentUserService currentUser)
        {
            _favoriteService = favoriteService;
            _currentUser = currentUser;
        }

        [Authorize]
        [HttpGet("/api/v1/me/favorites")]
        public async Task<IActionResult> GetMyFavorites()
        {
            var favorites = await _favoriteService
                .GetUserFavoritesAsync(_currentUser.UserId);

            return Ok(favorites);

            //var claims = User.Claims
            //    .Select(claims => new
            //    {
            //        claims.Type,
            //        claims.Value
            //    });

            //return Ok(claims);
        }

        [Authorize]
        [HttpPost("/api/v1/me/favorites/{movieId:int}")]
        public async Task<IActionResult> AddFavorite(int movieId)
        {
            var favorite = await _favoriteService.AddFavoriteAsync(_currentUser.UserId, movieId);

            if (favorite is null)
            {
                return NotFound();
            }

            return CreatedAtAction(nameof(GetMyFavorites), favorite);
        }

        [Authorize]
        [HttpPost("/api/v1/me/favorites/tmdb/{tmdbId:int}")]
        public async Task<ActionResult<FavoriteMovieResponse>> AddTmdbMovieToFavorite(
            int tmdbId, CancellationToken cancellationToken)
        {
            if (tmdbId <= 0)
            {
                return BadRequest("TMDB movie ID must be greater than zero.");
            }

            var favorite = await _favoriteService.AddFavoriteByTmdbIdAsync(
                _currentUser.UserId,
                tmdbId,
                cancellationToken);

            return Ok(favorite);
        }

        [Authorize]
        [HttpDelete("/api/v1/me/favorites/{movieId:int}")]
        public async Task<IActionResult> RemoveFavorite(int movieId)
        {
            var removed = await _favoriteService.RemoveFavoriteAsync(_currentUser.UserId,movieId);

            if (!removed)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
