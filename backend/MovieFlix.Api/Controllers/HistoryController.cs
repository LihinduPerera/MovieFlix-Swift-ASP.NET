using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.History;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.Models;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/me/history")]
    [Authorize]
    public class HistoryController : ApiControllerBase
    {
        private readonly IWatchHistoryService _historyService;

        public HistoryController(IWatchHistoryService historyService)
        {
            _historyService = historyService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResponse<WatchHistoryItemResponse>>> Get(
            [FromQuery] HistoryListQueryRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _historyService.GetHistoryAsync(
                RequireUserId(),
                request.MediaType,
                request.Page,
                request.PageSize,
                cancellationToken));
        }

        /// <summary>Unfinished movies and TV shows, newest first.</summary>
        [HttpGet("continue")]
        public async Task<ActionResult<List<WatchHistoryItemResponse>>> Continue(
            [FromQuery] ContinueWatchingQueryRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _historyService.GetContinueWatchingAsync(
                RequireUserId(),
                request.Limit,
                cancellationToken));
        }

        /// <summary>Tracked episode states for one TV show.</summary>
        [HttpGet("tv/{tmdbId:int}")]
        public async Task<ActionResult<TvHistoryResponse>> GetTvHistory(
            int tmdbId,
            CancellationToken cancellationToken)
        {
            var history = await _historyService.GetTvHistoryAsync(
                RequireUserId(),
                tmdbId,
                cancellationToken);

            return history is null
                ? NotFoundProblem(
                    $"No watch history exists for TV show {tmdbId}.")
                : Ok(history);
        }

        /// <summary>Records progress for a movie, or for a TV show's current episode.</summary>
        [HttpPut("{mediaType:mediaType}/{tmdbId:int}")]
        public async Task<ActionResult<WatchHistoryItemResponse>> RecordProgress(
            MediaType mediaType,
            int tmdbId,
            [FromBody] RecordProgressRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _historyService.RecordProgressAsync(
                RequireUserId(),
                mediaType,
                tmdbId,
                request,
                cancellationToken));
        }

        /// <summary>Records progress for one TV episode.</summary>
        [HttpPut("tv/{tmdbId:int}/episodes/{seasonNumber:int}/{episodeNumber:int}")]
        public async Task<ActionResult<EpisodeProgressResponse>> RecordEpisodeProgress(
            int tmdbId,
            int seasonNumber,
            int episodeNumber,
            [FromBody] RecordEpisodeProgressRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _historyService.RecordEpisodeProgressAsync(
                RequireUserId(),
                tmdbId,
                seasonNumber,
                episodeNumber,
                request,
                cancellationToken));
        }

        [HttpDelete("{mediaType:mediaType}/{tmdbId:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Remove(
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken)
        {
            await _historyService.RemoveAsync(
                RequireUserId(),
                mediaType,
                tmdbId,
                cancellationToken);

            return NoContent();
        }

        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Clear(CancellationToken cancellationToken)
        {
            await _historyService.ClearAsync(RequireUserId(), cancellationToken);
            return NoContent();
        }
    }
}
