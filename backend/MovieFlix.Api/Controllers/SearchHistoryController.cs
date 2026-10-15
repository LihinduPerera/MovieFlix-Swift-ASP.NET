using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.SearchHistory;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/[controller]")]
    public class SearchHistoryController : ControllerBase
    {
        private readonly ISearchHistoryService _searchHistoryService;
        private readonly ICurrentUserService _currentUser;

        public SearchHistoryController(
            ISearchHistoryService searchHistoryService,
            ICurrentUserService currentUser)
        {
            _searchHistoryService = searchHistoryService;
            _currentUser = currentUser;
        }

        /// <summary>Records a search term for the current user.</summary>
        [Authorize]
        [HttpPost("/api/v1/me/search-history")]
        public async Task<IActionResult> Record(RecordSearchRequest request)
        {
            var result = await _searchHistoryService.RecordAsync(
                _currentUser.UserId, request.Query);

            if (result is null)
            {
                return BadRequest("Search query must not be empty.");
            }

            return Ok(result);
        }

        /// <summary>Recent distinct search terms (newest first).</summary>
        [Authorize]
        [HttpGet("/api/v1/me/search-history")]
        public async Task<IActionResult> GetRecent([FromQuery] int limit = 10)
        {
            if (limit < 1) limit = 1;
            if (limit > 50) limit = 50;

            var result = await _searchHistoryService.GetRecentAsync(
                _currentUser.UserId, limit);

            return Ok(result);
        }

        /// <summary>Removes a single search-history entry.</summary>
        [Authorize]
        [HttpDelete("/api/v1/me/search-history/{entryId:int}")]
        public async Task<IActionResult> Remove(int entryId)
        {
            var removed = await _searchHistoryService.RemoveAsync(
                _currentUser.UserId, entryId);

            if (!removed)
            {
                return NotFound();
            }

            return NoContent();
        }

        /// <summary>Clears the user's entire search history.</summary>
        [Authorize]
        [HttpDelete("/api/v1/me/search-history")]
        public async Task<IActionResult> Clear()
        {
            await _searchHistoryService.ClearAsync(_currentUser.UserId);
            return NoContent();
        }
    }
}
