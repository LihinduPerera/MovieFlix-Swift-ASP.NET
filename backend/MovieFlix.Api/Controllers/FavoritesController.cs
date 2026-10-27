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
    [Route("api/v1/me/favorites")]
    [Authorize]
    public class FavoritesController : ApiControllerBase
    {
        private readonly IFavoriteService _favoriteService;

        public FavoritesController(IFavoriteService favoriteService)
        {
            _favoriteService = favoriteService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResponse<SavedMediaResponse>>> Get(
            [FromQuery] LibraryListQueryRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _favoriteService.GetAsync(
                RequireUserId(),
                request.MediaType,
                request.Page,
                request.PageSize,
                request.Sort,
                cancellationToken));
        }

        /// <summary>Adds a favorite. Repeating the request is a no-op.</summary>
        [HttpPut("{mediaType}/{tmdbId:int}")]
        public async Task<ActionResult<SavedMediaResponse>> Add(
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken)
        {
            return Ok(await _favoriteService.AddAsync(
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
            await _favoriteService.RemoveAsync(
                RequireUserId(),
                mediaType,
                tmdbId,
                cancellationToken);

            return NoContent();
        }
    }
}
