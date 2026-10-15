namespace MovieFlix.Api.Models;

/// <summary>
/// A persisted refresh token belonging to a user. Refresh tokens let the
/// client obtain a new short-lived access token without logging in again.
/// Tokens are stored <b>hashed</b> so a database leak does not expose
/// usable credentials.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the raw token value handed to the client.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>Set when the token is revoked (logout, rotation, reuse).</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Hash of the token that replaced this one during rotation.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsActive => RevokedAt is null && !IsExpired;
}
