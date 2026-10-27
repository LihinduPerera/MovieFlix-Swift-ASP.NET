using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.Models;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/genres")]
    public class GenresController : ApiControllerBase
    {
        private readonly IMediaService _mediaService;

        public GenresController(IMediaService mediaService)
        {
            _mediaService = mediaService;
        }

        /// <summary>Returns the TMDB genre list for movies or TV shows.</summary>
        [HttpGet("{mediaType}")]
        [OutputCache(PolicyName = "tmdb-genres")]
        [ProducesResponseType(typeof(List<GenreResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<GenreResponse>>> GetGenres(
            MediaType mediaType,
            [FromQuery] LanguageQueryRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _mediaService.GetGenresAsync(
                mediaType,
                request.Language,
                cancellationToken));
        }
    }
}
