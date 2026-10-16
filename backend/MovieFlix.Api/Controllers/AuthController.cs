using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieFlix.Api.DTOs.Auth;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ICurrentUserService _currentUser;

        public AuthController(
            IAuthService authService,
            ICurrentUserService currentUser)
        {
            _authService = authService;
            _currentUser = currentUser;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            await _authService.RegisterAsync(request);

            return StatusCode(StatusCodes.Status201Created);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var response = await _authService.LoginAsync(request);

            if (response is null)
            {
                return Unauthorized();
            }

            return Ok(response);
        }

        /// <summary>
        /// Exchanges a valid refresh token for a new access + refresh token pair.
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshRequest request)
        {
            var response = await _authService.RefreshAsync(request);

            if (response is null)
            {
                return Unauthorized();
            }

            return Ok(response);
        }

        /// <summary>
        /// Logs the current user out by revoking all of their refresh tokens.
        /// </summary>
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _authService.LogoutAsync(_currentUser.UserId);

            return NoContent();
        }
    }
}
