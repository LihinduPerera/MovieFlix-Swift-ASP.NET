using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.SearchHistory;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/me/search-history")]
    [Authorize]
    public class SearchHistoryController : ApiControllerBase
    {
        private readonly ISearchHistoryService _searchHistoryService;

        public SearchHistoryController(ISearchHistoryService searchHistoryService)
        {
            _searchHistoryService = searchHistoryService;
        }

        [HttpGet]
        public async Task<ActionResult<List<SearchHistoryResponse>>> Get(
            [FromQuery] int limit,
            CancellationToken cancellationToken)
        {
            return Ok(await _searchHistoryService.GetRecentAsync(
                RequireUserId(),
                limit <= 0 ? 10 : limit,
                cancellationToken));
        }

        [HttpPost]
        public async Task<ActionResult<SearchHistoryResponse>> Record(
            [FromBody] RecordSearchRequest request,
            CancellationToken cancellationToken)
        {
            var entry = await _searchHistoryService.RecordAsync(
                RequireUserId(),
                request.Query,
                cancellationToken);

            if (entry is null)
            {
                return ValidationProblem(
                    new ValidationProblemDetails(
                        new Dictionary<string, string[]>
                        {
                            ["Query"] = ["Query must not be empty."]
                        })
                    {
                        Type = ProblemTypes.Validation,
                        Title = "One or more validation errors occurred.",
                        Status = StatusCodes.Status400BadRequest
                    });
            }

            return Ok(entry);
        }

        [HttpDelete("{entryId:int}")]
        public async Task<IActionResult> Remove(
            int entryId,
            CancellationToken cancellationToken)
        {
            var removed = await _searchHistoryService.RemoveAsync(
                RequireUserId(),
                entryId,
                cancellationToken);

            return removed
                ? NoContent()
                : NotFoundProblem(
                    $"Search history entry {entryId} was not found.");
        }

        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Clear(CancellationToken cancellationToken)
        {
            await _searchHistoryService.ClearAsync(
                RequireUserId(),
                cancellationToken);

            return NoContent();
        }
    }
}
