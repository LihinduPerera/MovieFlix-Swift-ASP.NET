using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.History;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class HistoryController : ControllerBase
    {
        private readonly IWatchHistoryService _historyService;
        private readonly ICurrentUserService _currentUser;

        public HistoryController(
            IWatchHistoryService historyService,
            ICurrentUserService currentUser)
        {
            _historyService = historyService;
            _currentUser = currentUser;
        }

        /// <summary>Records or updates playback progress for a movie.</summary>
        [Authorize]
        [HttpPut("/api/v1/me/history/{movieId:int}")]
        public async Task<IActionResult> RecordProgress(
            int movieId, RecordProgressRequest request)
        {
            var result = await _historyService.RecordProgressAsync(
                _currentUser.UserId,
                movieId,
                request.ProgressSeconds,
                request.Completed);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        /// <summary>Recently watched movies, newest first (paginated).</summary>
        [Authorize]
        [HttpGet("/api/v1/me/history")]
        public async Task<IActionResult> GetRecentlyWatched(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 100) pageSize = 100;

            var result = await _historyService.GetRecentlyWatchedAsync(
                _currentUser.UserId, page, pageSize);

            return Ok(result);
        }

        /// <summary>Started-but-unfinished movies for the Continue Watching rail.</summary>
        [Authorize]
        [HttpGet("/api/v1/me/history/continue")]
        public async Task<IActionResult> GetContinueWatching()
        {
            var result = await _historyService.GetContinueWatchingAsync(
                _currentUser.UserId);

            return Ok(result);
        }

        /// <summary>Removes a single movie from the user's history.</summary>
        [Authorize]
        [HttpDelete("/api/v1/me/history/{movieId:int}")]
        public async Task<IActionResult> Remove(int movieId)
        {
            var removed = await _historyService.RemoveAsync(
                _currentUser.UserId, movieId);

            if (!removed)
            {
                return NotFound();
            }

            return NoContent();
        }

        /// <summary>Clears the user's entire watch history.</summary>
        [Authorize]
        [HttpDelete("/api/v1/me/history")]
        public async Task<IActionResult> Clear()
        {
            await _historyService.ClearAsync(_currentUser.UserId);
            return NoContent();
        }
    }
}
