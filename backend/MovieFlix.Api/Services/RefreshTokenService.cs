using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Data;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>
    /// Hashed refresh-token storage with atomic rotation. The conditional
    /// ExecuteUpdate guarantees that two concurrent refresh requests cannot
    /// both consume the same token.
    /// </summary>
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly MovieFlixDbContext _context;
        private readonly JwtSettings _settings;
        private readonly TimeProvider _timeProvider;

        public RefreshTokenService(
            MovieFlixDbContext context,
            IOptions<JwtSettings> options,
            TimeProvider timeProvider)
        {
            _context = context;
            _settings = options.Value;
            _timeProvider = timeProvider;
        }

        public async Task<IssuedRefreshToken> IssueAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            var rawToken = CreateSecureToken();
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expiresAt = now.AddDays(_settings.RefreshTokenDays);

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                TokenHash = HashToken(rawToken),
                CreatedAt = now,
                ExpiresAt = expiresAt
            });

            await _context.SaveChangesAsync(cancellationToken);

            return new IssuedRefreshToken(rawToken, expiresAt);
        }

        public async Task<RotatedRefreshToken?> RotateAsync(
            string token,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var tokenHash = HashToken(token);
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            await using var transaction = await _context.Database
                .BeginTransactionAsync(cancellationToken);

            var entity = await _context.RefreshTokens
                .FirstOrDefaultAsync(
                    item => item.TokenHash == tokenHash,
                    cancellationToken);

            if (entity is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            if (entity.RevokedAt is not null)
            {
                // Reuse of a rotated/revoked token: burn the whole chain.
                await RevokeAllInternalAsync(entity.UserId, now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            if (entity.ExpiresAt <= now)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            var newRawToken = CreateSecureToken();
            var newTokenHash = HashToken(newRawToken);
            var expiresAt = now.AddDays(_settings.RefreshTokenDays);

            var affected = await _context.RefreshTokens
                .Where(item => item.Id == entity.Id && item.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.RevokedAt, now)
                        .SetProperty(item => item.ReplacedByTokenHash, newTokenHash),
                    cancellationToken);

            if (affected == 0)
            {
                // Another request won the race. Treat it as reuse.
                await RevokeAllInternalAsync(entity.UserId, now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = entity.UserId,
                TokenHash = newTokenHash,
                CreatedAt = now,
                ExpiresAt = expiresAt
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new RotatedRefreshToken(entity.UserId, newRawToken, expiresAt);
        }

        public async Task RevokeAllAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            await RevokeAllInternalAsync(userId, now, cancellationToken);
        }

        private Task<int> RevokeAllInternalAsync(
            string userId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            return _context.RefreshTokens
                .Where(item => item.UserId == userId && item.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(item => item.RevokedAt, now),
                    cancellationToken);
        }

        private static string CreateSecureToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string HashToken(string token)
        {
            var hash = SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(token));

            return Convert.ToHexString(hash);
        }
    }
}
