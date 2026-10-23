using MovieFlix.Api.DTOs.Auth;

namespace MovieFlix.Api.Services
{
    public interface IAuthService
    {
        /// <summary>
        /// Registers a new user. Throws <see cref="Exceptions.UserAlreadyExistsException"/>
        /// when the email is taken; Identity validation failures throw
        /// <see cref="Exceptions.IdentityValidationException"/>.
        /// </summary>
        Task RegisterAsync(RegisterRequest request);

        /// <summary>
        /// Logs a user in and returns an access + refresh token pair, or null
        /// when the credentials are invalid.
        /// </summary>
        Task<LoginResponse?> LoginAsync(LoginRequest request);

        /// <summary>
        /// Exchanges a valid refresh token for a new access + refresh token pair
        /// (refresh token rotation). Returns null when the token is invalid.
        /// </summary>
        Task<LoginResponse?> RefreshAsync(RefreshRequest request);

        /// <summary>Logs the current user out by revoking their refresh tokens.</summary>
        Task LogoutAsync(string userId);
    }
}
