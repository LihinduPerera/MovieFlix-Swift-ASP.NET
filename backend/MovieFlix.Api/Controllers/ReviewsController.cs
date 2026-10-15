using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.Reviews;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _reviewService;
        private readonly ICurrentUserService _currentUser;

        public ReviewsController(
            IReviewService reviewService,
            ICurrentUserService currentUser)
        {
            _reviewService = reviewService;
            _currentUser = currentUser;
        }

        /// <summary>Public list of a movie's reviews (paginated).</summary>
        [HttpGet("/api/v1/movies/{movieId:int}/reviews")]
        public async Task<IActionResult> GetForMovie(
            int movieId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 100) pageSize = 100;

            var result = await _reviewService.GetForMovieAsync(
                movieId, page, pageSize);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        /// <summary>All reviews written by the current user.</summary>
        [Authorize]
        [HttpGet("/api/v1/me/reviews")]
        public async Task<IActionResult> GetMine()
        {
            var result = await _reviewService.GetForUserAsync(_currentUser.UserId);
            return Ok(result);
        }

        /// <summary>Creates a review for a movie.</summary>
        [Authorize]
        [HttpPost("/api/v1/movies/{movieId:int}/reviews")]
        public async Task<IActionResult> Create(
            int movieId, CreateReviewRequest request)
        {
            var result = await _reviewService.CreateAsync(
                _currentUser.UserId, movieId, request.Content);

            if (result is null)
            {
                return NotFound();
            }

            return CreatedAtAction(
                nameof(GetForMovie),
                new { movieId },
                result);
        }

        /// <summary>Updates the current user's own review.</summary>
        [Authorize]
        [HttpPut("/api/v1/reviews/{reviewId:int}")]
        public async Task<IActionResult> Update(
            int reviewId, UpdateReviewRequest request)
        {
            var result = await _reviewService.UpdateAsync(
                _currentUser.UserId, reviewId, request.Content);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        /// <summary>Deletes the current user's own review.</summary>
        [Authorize]
        [HttpDelete("/api/v1/reviews/{reviewId:int}")]
        public async Task<IActionResult> Delete(int reviewId)
        {
            var deleted = await _reviewService.DeleteAsync(
                _currentUser.UserId, reviewId);

            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
