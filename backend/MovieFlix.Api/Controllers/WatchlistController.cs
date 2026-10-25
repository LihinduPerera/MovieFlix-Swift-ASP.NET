using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.Models;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/me/watchlist")]
    [Authorize]
    public class WatchlistController : ApiControllerBase
    {
        private readonly IWatchlistService _watchlistService;

        public WatchlistController(IWatchlistService watchlistService)
        {
            _watchlistService = watchlistService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResponse<SavedMediaResponse>>> Get(
            [FromQuery] LibraryListQueryRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _watchlistService.GetAsync(
                RequireUserId(),
                request.MediaType,
                request.Page,
                request.PageSize,
                request.Sort,
                cancellationToken));
        }

        [HttpPut("{mediaType}/{tmdbId:int}")]
        public async Task<ActionResult<SavedMediaResponse>> Add(
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken)
        {
            return Ok(await _watchlistService.AddAsync(
                RequireUserId(),
                mediaType,
                tmdbId,
                cancellationToken));
        }

        [HttpDelete("{mediaType}/{tmdbId:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Remove(
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken)
        {
            await _watchlistService.RemoveAsync(
                RequireUserId(),
                mediaType,
                tmdbId,
                cancellationToken);

            return NoContent();
        }
    }
}
