using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.Collections;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.Models;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1")]
    public class CollectionsController : ApiControllerBase
    {
        private readonly ICollectionService _collectionService;

        public CollectionsController(ICollectionService collectionService)
        {
            _collectionService = collectionService;
        }

        [HttpGet("me/collections")]
        [Authorize]
        public async Task<ActionResult<PagedResponse<CollectionSummaryResponse>>> GetMine(
            [FromQuery] PagedQueryRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _collectionService.GetUserCollectionsAsync(
                RequireUserId(),
                request.Page,
                request.PageSize,
                cancellationToken));
        }

        /// <summary>Public collection detail. Private collections are still readable by id.</summary>
        [HttpGet("collections/{collectionId:int}")]
        public async Task<ActionResult<CollectionDetailResponse>> Get(
            int collectionId,
            CancellationToken cancellationToken)
        {
            var collection = await _collectionService.GetByIdAsync(
                collectionId,
                CurrentUserId,
                cancellationToken);

            return collection is null
                ? NotFoundProblem($"Collection {collectionId} was not found.")
                : Ok(collection);
        }

        [HttpPost("me/collections")]
        [Authorize]
        [ProducesResponseType(typeof(CollectionSummaryResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<CollectionSummaryResponse>> Create(
            [FromBody] CreateCollectionRequest request,
            CancellationToken cancellationToken)
        {
            var collection = await _collectionService.CreateAsync(
                RequireUserId(),
                request,
                cancellationToken);

            return Created($"/api/v1/collections/{collection.Id}", collection);
        }

        [HttpPut("me/collections/{collectionId:int}")]
        [Authorize]
        public async Task<ActionResult<CollectionSummaryResponse>> Update(
            int collectionId,
            [FromBody] UpdateCollectionRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _collectionService.UpdateAsync(
                RequireUserId(),
                collectionId,
                request,
                cancellationToken));
        }

        [HttpDelete("me/collections/{collectionId:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Delete(
            int collectionId,
            CancellationToken cancellationToken)
        {
            await _collectionService.DeleteAsync(
                RequireUserId(),
                collectionId,
                cancellationToken);

            return NoContent();
        }

        /// <summary>Adds a title to a collection. Repeating the request is a no-op.</summary>
        [HttpPut("me/collections/{collectionId:int}/items/{mediaType:mediaType}/{tmdbId:int}")]
        [Authorize]
        public async Task<ActionResult<CollectionDetailResponse>> AddItem(
            int collectionId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken)
        {
            return Ok(await _collectionService.AddItemAsync(
                RequireUserId(),
                collectionId,
                mediaType,
                tmdbId,
                cancellationToken));
        }

        [HttpDelete("me/collections/{collectionId:int}/items/{mediaType:mediaType}/{tmdbId:int}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> RemoveItem(
            int collectionId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken)
        {
            await _collectionService.RemoveItemAsync(
                RequireUserId(),
                collectionId,
                mediaType,
                tmdbId,
                cancellationToken);

            return NoContent();
        }
    }
}
