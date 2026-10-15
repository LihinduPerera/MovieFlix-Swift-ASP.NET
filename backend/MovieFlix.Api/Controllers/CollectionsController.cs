using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.Collections;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class CollectionsController : ControllerBase
    {
        private readonly ICollectionService _collectionService;
        private readonly ICurrentUserService _currentUser;

        public CollectionsController(
            ICollectionService collectionService,
            ICurrentUserService currentUser)
        {
            _collectionService = collectionService;
            _currentUser = currentUser;
        }

        /// <summary>The current user's collections.</summary>
        [Authorize]
        [HttpGet("/api/v1/me/collections")]
        public async Task<IActionResult> GetMine()
        {
            var collections = await _collectionService
                .GetUserCollectionsAsync(_currentUser.UserId);

            return Ok(collections);
        }

        /// <summary>A collection with its movies (public read).</summary>
        [HttpGet("/api/v1/collections/{collectionId:int}")]
        public async Task<IActionResult> GetById(int collectionId)
        {
            var collection = await _collectionService.GetByIdAsync(collectionId);

            if (collection is null)
            {
                return NotFound();
            }

            return Ok(collection);
        }

        [Authorize]
        [HttpPost("/api/v1/me/collections")]
        public async Task<IActionResult> Create(CreateCollectionRequest request)
        {
            var created = await _collectionService.CreateAsync(
                _currentUser.UserId, request.Name, request.Description);

            return CreatedAtAction(
                nameof(GetById),
                new { collectionId = created.Id },
                created);
        }

        [Authorize]
        [HttpPut("/api/v1/me/collections/{collectionId:int}")]
        public async Task<IActionResult> Update(
            int collectionId, UpdateCollectionRequest request)
        {
            var updated = await _collectionService.UpdateAsync(
                _currentUser.UserId, collectionId, request.Name, request.Description);

            if (updated is null)
            {
                return NotFound();
            }

            return Ok(updated);
        }

        [Authorize]
        [HttpDelete("/api/v1/me/collections/{collectionId:int}")]
        public async Task<IActionResult> Delete(int collectionId)
        {
            var deleted = await _collectionService.DeleteAsync(
                _currentUser.UserId, collectionId);

            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }

        [Authorize]
        [HttpPost("/api/v1/me/collections/{collectionId:int}/movies/{movieId:int}")]
        public async Task<IActionResult> AddMovie(int collectionId, int movieId)
        {
            var result = await _collectionService.AddMovieAsync(
                _currentUser.UserId, collectionId, movieId);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        [Authorize]
        [HttpDelete("/api/v1/me/collections/{collectionId:int}/movies/{movieId:int}")]
        public async Task<IActionResult> RemoveMovie(int collectionId, int movieId)
        {
            var removed = await _collectionService.RemoveMovieAsync(
                _currentUser.UserId, collectionId, movieId);

            if (!removed)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
