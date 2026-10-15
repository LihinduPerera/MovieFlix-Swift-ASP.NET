using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public interface IRefreshTokenService
    {
        /// <summary>
        /// Issues a new refresh token for the user, persists its hash, and
        /// returns the plaintext token to hand to the client.
        /// </summary>
        Task<string> IssueAsync(
            string userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a plaintext refresh token and returns the owning user's ID,
        /// or null when the token is unknown, revoked, or expired.
        /// </summary>
        Task<string?> ValidateAsync(
            string token,
            CancellationToken cancellationToken = default);

        /// <summary>Rotates a token: revokes the old one and issues a replacement.</summary>
        Task<string> RotateAsync(
            string token,
            CancellationToken cancellationToken = default);

        /// <summary>Revokes every active refresh token for a user (full logout).</summary>
        Task RevokeAllAsync(
            string userId,
            CancellationToken cancellationToken = default);
    }
}
