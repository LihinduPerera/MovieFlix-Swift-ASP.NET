using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.DTOs.Reviews;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1")]
    public class ReviewsController : ApiControllerBase
    {
        private readonly IReviewService _reviewService;

        public ReviewsController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        /// <summary>All reviews written by the authenticated user.</summary>
        [HttpGet("me/reviews")]
        [Authorize]
        public async Task<ActionResult<PagedResponse<ReviewResponse>>> GetMine(
            [FromQuery] PagedQueryRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _reviewService.GetForUserAsync(
                RequireUserId(),
                request.Page,
                request.PageSize,
                cancellationToken));
        }

        [HttpPut("reviews/{reviewId:int}")]
        [Authorize]
        public async Task<ActionResult<ReviewResponse>> Update(
            int reviewId,
            [FromBody] UpdateReviewRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _reviewService.UpdateAsync(
                RequireUserId(),
                reviewId,
                request,
                cancellationToken));
        }

        [HttpDelete("reviews/{reviewId:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Delete(
            int reviewId,
            CancellationToken cancellationToken)
        {
            await _reviewService.DeleteAsync(
                RequireUserId(),
                reviewId,
                cancellationToken);

            return NoContent();
        }
    }
}
