using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.Ratings;
using MovieFlix.Api.Models;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/me/ratings")]
    [Authorize]
    public class RatingsController : ApiControllerBase
    {
        private readonly IRatingService _ratingService;

        public RatingsController(IRatingService ratingService)
        {
            _ratingService = ratingService;
        }

        [HttpGet("{mediaType}/{tmdbId:int}")]
        [ProducesResponseType(typeof(MediaRatingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MediaRatingResponse>> Get(
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken)
        {
            var rating = await _ratingService.GetMyRatingAsync(
                RequireUserId(),
                mediaType,
                tmdbId,
                cancellationToken);

            return rating is null
                ? NotFoundProblem("You have not rated this title.")
                : Ok(rating);
        }

        [HttpPut("{mediaType}/{tmdbId:int}")]
        public async Task<ActionResult<MediaRatingResponse>> Rate(
            MediaType mediaType,
            int tmdbId,
            [FromBody] RatingRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _ratingService.RateAsync(
                RequireUserId(),
                mediaType,
                tmdbId,
                request.Rating,
                cancellationToken));
        }

        [HttpDelete("{mediaType}/{tmdbId:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Remove(
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken)
        {
            await _ratingService.RemoveAsync(
                RequireUserId(),
                mediaType,
                tmdbId,
                cancellationToken);

            return NoContent();
        }
    }
}
