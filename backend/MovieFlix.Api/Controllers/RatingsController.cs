using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.Ratings;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/[controller]")]
    public class RatingsController : ControllerBase
    {
        private readonly IRatingService _ratingService;
        private readonly ICurrentUserService _currentUser;

        public RatingsController(
            IRatingService ratingService,
            ICurrentUserService currentUser)
        {
            _ratingService = ratingService;
            _currentUser = currentUser;
        }

        /// <summary>Creates or updates the current user's rating for a movie.</summary>
        [Authorize]
        [HttpPut("/api/v1/me/ratings/{movieId:int}")]
        public async Task<IActionResult> Rate(
            int movieId, RateMovieRequest request)
        {
            var result = await _ratingService.RateAsync(
                _currentUser.UserId, movieId, request.Rating);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        /// <summary>Gets the current user's rating for a movie.</summary>
        [Authorize]
        [HttpGet("/api/v1/me/ratings/{movieId:int}")]
        public async Task<IActionResult> GetMyRating(int movieId)
        {
            var result = await _ratingService.GetMyRatingAsync(
                _currentUser.UserId, movieId);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        /// <summary>Removes the current user's rating for a movie.</summary>
        [Authorize]
        [HttpDelete("/api/v1/me/ratings/{movieId:int}")]
        public async Task<IActionResult> Remove(int movieId)
        {
            var removed = await _ratingService.RemoveAsync(
                _currentUser.UserId, movieId);

            if (!removed)
            {
                return NotFound();
            }

            return NoContent();
        }

        /// <summary>
        /// Community average rating for a movie (includes the caller's own
        /// rating when authenticated).
        /// </summary>
        [HttpGet("/api/v1/movies/{movieId:int}/ratings")]
        public async Task<IActionResult> GetCommunityRating(int movieId)
        {
            // The caller may be anonymous; only attach their rating if signed in.
            var userId = User.Identity?.IsAuthenticated == true
                ? _currentUser.UserId
                : null;

            var result = await _ratingService.GetCommunityRatingAsync(
                userId, movieId);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }
    }
}
