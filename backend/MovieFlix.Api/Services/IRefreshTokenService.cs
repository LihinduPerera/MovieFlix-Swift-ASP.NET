namespace MovieFlix.Api.Services
{
    public record IssuedRefreshToken(string Token, DateTime ExpiresAt);

    public record RotatedRefreshToken(
        string UserId,
        string Token,
        DateTime ExpiresAt);

    public interface IRefreshTokenService
    {
        Task<IssuedRefreshToken> IssueAsync(
            string userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically consumes a refresh token and issues its replacement.
        /// Returns null for unknown, expired or reused tokens. Reuse revokes the
        /// user's whole token chain.
        /// </summary>
        Task<RotatedRefreshToken?> RotateAsync(
            string token,
            CancellationToken cancellationToken = default);

        Task RevokeAllAsync(
            string userId,
            CancellationToken cancellationToken = default);
    }
}
