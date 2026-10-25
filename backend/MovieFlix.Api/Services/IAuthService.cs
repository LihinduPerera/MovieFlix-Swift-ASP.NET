using MovieFlix.Api.DTOs.Auth;

namespace MovieFlix.Api.Services
{
    public interface IAuthService
    {
        /// <summary>Registers a user and returns an initial token pair.</summary>
        Task<LoginResponse> RegisterAsync(
            RegisterRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>Returns null when the credentials are invalid.</summary>
        Task<LoginResponse?> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>Rotates a refresh token and issues a new access token.</summary>
        Task<LoginResponse?> RefreshAsync(
            RefreshRequest request,
            CancellationToken cancellationToken = default);

        Task LogoutAsync(
            string userId,
            CancellationToken cancellationToken = default);
    }
}
