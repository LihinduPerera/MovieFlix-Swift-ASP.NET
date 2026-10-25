using Microsoft.AspNetCore.Identity;
using MovieFlix.Api.DTOs.Auth;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IJwtService _jwtService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly TimeProvider _timeProvider;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            IJwtService jwtService,
            IRefreshTokenService refreshTokenService,
            TimeProvider timeProvider)
        {
            _userManager = userManager;
            _jwtService = jwtService;
            _refreshTokenService = refreshTokenService;
            _timeProvider = timeProvider;
        }

        public async Task<LoginResponse> RegisterAsync(
            RegisterRequest request,
            CancellationToken cancellationToken = default)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            var existing = await _userManager.FindByEmailAsync(email);

            if (existing is not null)
            {
                throw new UserAlreadyExistsException(email);
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = request.DisplayName.Trim(),
                EmailConfirmed = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                if (result.Errors.Any(error =>
                    error.Code is "DuplicateEmail" or "DuplicateUserName"))
                {
                    throw new UserAlreadyExistsException(email);
                }

                throw new IdentityValidationException(result.Errors);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "User");

            if (!roleResult.Succeeded)
            {
                throw new IdentityValidationException(roleResult.Errors);
            }

            return await IssueResponseAsync(user, cancellationToken: cancellationToken);
        }

        public async Task<LoginResponse?> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _userManager.FindByEmailAsync(email);

            if (user is null)
            {
                return null;
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                throw new LockedOutException();
            }

            var passwordValid = await _userManager.CheckPasswordAsync(
                user,
                request.Password);

            if (!passwordValid)
            {
                await _userManager.AccessFailedAsync(user);
                return null;
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            return await IssueResponseAsync(user, cancellationToken: cancellationToken);
        }

        public async Task<LoginResponse?> RefreshAsync(
            RefreshRequest request,
            CancellationToken cancellationToken = default)
        {
            var rotated = await _refreshTokenService.RotateAsync(
                request.RefreshToken,
                cancellationToken);

            if (rotated is null)
            {
                return null;
            }

            var user = await _userManager.FindByIdAsync(rotated.UserId);

            if (user is null)
            {
                return null;
            }

            return await IssueResponseAsync(
                user,
                rotated,
                cancellationToken);
        }

        public Task LogoutAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            return _refreshTokenService.RevokeAllAsync(userId, cancellationToken);
        }

        private async Task<LoginResponse> IssueResponseAsync(
            ApplicationUser user,
            RotatedRefreshToken? rotated = null,
            CancellationToken cancellationToken = default)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var accessToken = _jwtService.GenerateToken(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                roles);

            string rawRefreshToken;
            DateTime refreshTokenExpiresAt;

            if (rotated is not null)
            {
                rawRefreshToken = rotated.Token;
                refreshTokenExpiresAt = rotated.ExpiresAt;
            }
            else
            {
                var issued = await _refreshTokenService.IssueAsync(
                    user.Id,
                    cancellationToken);

                rawRefreshToken = issued.Token;
                refreshTokenExpiresAt = issued.ExpiresAt;
            }

            return new LoginResponse
            {
                AccessToken = accessToken,
                AccessTokenExpiresAt = now.Add(_jwtService.AccessTokenLifetime),
                RefreshToken = rawRefreshToken,
                RefreshTokenExpiresAt = refreshTokenExpiresAt,
                TokenType = "Bearer",
                User = new UserResponse
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    DisplayName = user.DisplayName,
                    CreatedAt = user.CreatedAt,
                    Roles = roles.ToList()
                }
            };
        }
    }
}
