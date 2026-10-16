using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Data;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly MovieFlixDbContext _context;
        private readonly JwtSettings _jwtSettings;

        public RefreshTokenService(
            MovieFlixDbContext context,
            IOptions<JwtSettings> jwtOptions)
        {
            _context = context;
            _jwtSettings = jwtOptions.Value;
        }

        public async Task<string> IssueAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            var rawToken = CreateSecureToken();
            var tokenHash = HashToken(rawToken);

            var entity = new RefreshToken
            {
                UserId = userId,
                TokenHash = tokenHash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays)
            };

            _context.RefreshTokens.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);

            return rawToken;
        }

        public async Task<string?> ValidateAsync(
            string token,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var tokenHash = HashToken(token);

            var entity = await _context.RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.TokenHash == tokenHash,
                    cancellationToken);

            if (entity is null || !entity.IsActive)
            {
                return null;
            }

            return entity.UserId;
        }

        public async Task<string> RotateAsync(
            string token,
            CancellationToken cancellationToken = default)
        {
            var tokenHash = HashToken(token);

            var entity = await _context.RefreshTokens
                .FirstOrDefaultAsync(
                    t => t.TokenHash == tokenHash,
                    cancellationToken);

            // Reuse of an already-rotated token is suspicious: revoke the whole
            // chain for that user to limit damage from a stolen token.
            if (entity is null || entity.RevokedAt is not null)
            {
                if (entity is not null)
                {
                    await RevokeAllAsync(entity.UserId, cancellationToken);
                }

                throw new InvalidOperationException("Refresh token is invalid or already used.");
            }

            if (entity.IsExpired)
            {
                throw new InvalidOperationException("Refresh token has expired.");
            }

            var newRawToken = CreateSecureToken();
            var newTokenHash = HashToken(newRawToken);

            entity.RevokedAt = DateTime.UtcNow;
            entity.ReplacedByTokenHash = newTokenHash;

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = entity.UserId,
                TokenHash = newTokenHash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays)
            });

            await _context.SaveChangesAsync(cancellationToken);

            return newRawToken;
        }

        public async Task RevokeAllAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            var activeTokens = await _context.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ToListAsync(cancellationToken);

            if (activeTokens.Count == 0)
            {
                return;
            }

            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private static string CreateSecureToken()
        {
            // 64 random bytes => ~86-char Base64Url string (~512 bits of entropy).
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string HashToken(string token)
        {
            var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hash);
        }
    }
}
