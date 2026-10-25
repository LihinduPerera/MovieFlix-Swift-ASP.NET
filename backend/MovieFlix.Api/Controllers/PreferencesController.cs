using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.Preferences;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/me/preferences")]
    [Authorize]
    public class PreferencesController : ApiControllerBase
    {
        private readonly IPreferencesService _preferencesService;

        public PreferencesController(IPreferencesService preferencesService)
        {
            _preferencesService = preferencesService;
        }

        [HttpGet]
        public async Task<ActionResult<PreferencesResponse>> Get(
            CancellationToken cancellationToken)
        {
            var preferences = await _preferencesService.GetAsync(
                RequireUserId(),
                cancellationToken);

            return preferences is null
                ? NotFoundProblem("Preferences were not found for this user.")
                : Ok(preferences);
        }

        [HttpPut]
        public async Task<ActionResult<PreferencesResponse>> Update(
            [FromBody] UpdatePreferencesRequest request,
            CancellationToken cancellationToken)
        {
            var preferences = await _preferencesService.UpdateAsync(
                RequireUserId(),
                request,
                cancellationToken);

            return preferences is null
                ? NotFoundProblem("Preferences were not found for this user.")
                : Ok(preferences);
        }
    }
}
