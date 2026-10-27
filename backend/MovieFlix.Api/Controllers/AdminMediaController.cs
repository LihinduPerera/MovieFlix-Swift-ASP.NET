using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Models;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/admin/media")]
    [Authorize(Roles = "Admin")]
    public class AdminMediaController : ApiControllerBase
    {
        private readonly IMediaCacheService _cacheService;
        private readonly TmdbSettings _tmdbSettings;

        public AdminMediaController(
            IMediaCacheService cacheService,
            IOptions<TmdbSettings> tmdbSettings)
        {
            _cacheService = cacheService;
            _tmdbSettings = tmdbSettings.Value;
        }

        /// <summary>Forces a metadata refresh from TMDB.</summary>
        [HttpPost("{mediaType}/{tmdbId:int}/refresh")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Refresh(
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken)
        {
            await _cacheService.RefreshAsync(
                mediaType,
                tmdbId,
                _tmdbSettings.DefaultLanguage,
                cancellationToken);

            return NoContent();
        }
    }
}
