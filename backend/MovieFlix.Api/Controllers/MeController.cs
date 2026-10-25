using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.Profile;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/me")]
    [Authorize]
    public class MeController : ApiControllerBase
    {
        private readonly IProfileService _profileService;

        public MeController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        /// <summary>Returns the authenticated user's profile.</summary>
        [HttpGet]
        public async Task<ActionResult<MeResponse>> Get(CancellationToken cancellationToken)
        {
            var profile = await _profileService.GetAsync(
                RequireUserId(),
                cancellationToken);

            return profile is null
                ? NotFoundProblem("The requested user was not found.")
                : Ok(profile);
        }

        /// <summary>Updates the authenticated user's display name.</summary>
        [HttpPatch]
        public async Task<ActionResult<MeResponse>> Update(
            [FromBody] UpdateProfileRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _profileService.UpdateAsync(
                RequireUserId(),
                request,
                cancellationToken));
        }

        /// <summary>Returns aggregate library and watch statistics.</summary>
        [HttpGet("stats")]
        public async Task<ActionResult<StatsResponse>> GetStats(
            CancellationToken cancellationToken)
        {
            return Ok(await _profileService.GetStatsAsync(
                RequireUserId(),
                cancellationToken));
        }
    }
}
