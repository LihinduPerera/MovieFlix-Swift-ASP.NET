using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.Preferences;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/[controller]")]
    public class PreferencesController : ControllerBase
    {
        private readonly IPreferencesService _preferencesService;
        private readonly ICurrentUserService _currentUser;

        public PreferencesController(
            IPreferencesService preferencesService,
            ICurrentUserService currentUser)
        {
            _preferencesService = preferencesService;
            _currentUser = currentUser;
        }

        /// <summary>Returns the current user's preferences (creates defaults on first call).</summary>
        [Authorize]
        [HttpGet("/api/v1/me/preferences")]
        public async Task<IActionResult> Get()
        {
            var result = await _preferencesService.GetAsync(_currentUser.UserId);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        /// <summary>Updates the current user's preferences.</summary>
        [Authorize]
        [HttpPut("/api/v1/me/preferences")]
        public async Task<IActionResult> Update(UpdatePreferencesRequest request)
        {
            var result = await _preferencesService.UpdateAsync(
                _currentUser.UserId, request);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }
    }
}
