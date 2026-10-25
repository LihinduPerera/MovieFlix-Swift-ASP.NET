using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/search")]
    public class SearchController : ApiControllerBase
    {
        private readonly IMediaService _mediaService;

        public SearchController(IMediaService mediaService)
        {
            _mediaService = mediaService;
        }

        /// <summary>
        /// Searches movies, TV shows, or both. Persons are never returned.
        /// </summary>
        [HttpGet]
        [OutputCache(PolicyName = "tmdb-search")]
        [ProducesResponseType(typeof(PagedResponse<MediaSummaryResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResponse<MediaSummaryResponse>>> Search(
            [FromQuery] SearchQueryRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _mediaService.SearchAsync(request, cancellationToken));
        }
    }
}
