using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Auth;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public class AuthService : IAuthService
    {
        private readonly MovieFlixDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IJwtService _jwtService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly JwtSettings _jwtSettings;

        public AuthService(
            MovieFlixDbContext context,
            UserManager<ApplicationUser> userManager,
            IJwtService jwtService,
            IRefreshTokenService refreshTokenService,
            IOptions<JwtSettings> jwtOptions)
        {
            _context = context;
            _userManager = userManager;
            _jwtService = jwtService;
            _refreshTokenService = refreshTokenService;
            _jwtSettings = jwtOptions.Value;
        }

        public async Task RegisterAsync(RegisterRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            var emailExists = await _context.Users
                .AnyAsync(user => user.Email == email);

            if (emailExists)
            {
                throw new UserAlreadyExistsException(email);
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                NormalizedUserName = email.ToUpperInvariant(),
                DisplayName = request.DisplayName.Trim(),
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Identity hashes the password and enforces the configured password
            // policy in one call.
            var result = await _userManager.CreateAsync(
                user,
                request.Password);

            if (!result.Succeeded)
            {
                throw new IdentityValidationException(result.Errors);
            }

            // Every new account starts in the "User" role.
            await _userManager.AddToRoleAsync(user, "User");
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            var user = await _userManager.FindByEmailAsync(email);

            if (user is null)
            {
                return null;
            }

            // CheckPasswordAsync verifies the stored hash without side effects.
            var passwordValid = await _userManager.CheckPasswordAsync(
                user,
                request.Password);

            if (!passwordValid)
            {
                return null;
            }

            return await IssueTokenResponseAsync(user);
        }

        public async Task<LoginResponse?> RefreshAsync(RefreshRequest request)
        {
            var userId = await _refreshTokenService.ValidateAsync(
                request.RefreshToken);

            if (userId is null)
            {
                return null;
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user is null)
            {
                return null;
            }

            // Rotate first: revoke the presented token and issue a replacement.
            var newRefreshToken = await _refreshTokenService.RotateAsync(
                request.RefreshToken);

            var response = await IssueTokenResponseAsync(
                user,
                existingRefreshToken: newRefreshToken);

            return response;
        }

        public async Task LogoutAsync(string userId)
        {
            await _refreshTokenService.RevokeAllAsync(userId);
        }

        private async Task<LoginResponse> IssueTokenResponseAsync(
            ApplicationUser user,
            string? existingRefreshToken = null)
        {
            var roles = await _userManager.GetRolesAsync(user);

            var accessToken = _jwtService.GenerateToken(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                roles);

            // When refreshing, the rotation already produced a fresh token.
            var refreshToken = existingRefreshToken
                ?? await _refreshTokenService.IssueAsync(user.Id);

            var refreshExpiry = DateTime.UtcNow
                .AddDays(_jwtSettings.RefreshTokenDays);

            return new LoginResponse
            {
                AccessToken = accessToken,
                AccessTokenExpiresAt =
                    DateTime.UtcNow.Add(_jwtService.AccessTokenLifetime),
                RefreshToken = refreshToken,
                RefreshTokenExpiresAt = refreshExpiry,
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
