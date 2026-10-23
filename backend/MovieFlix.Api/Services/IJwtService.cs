namespace MovieFlix.Api.Services
{
    public interface IJwtService
    {
        string GenerateToken(
            string userId,
            string email,
            string displayName,
            IEnumerable<string> roles);

        /// <summary>Lifetime of the access token this service issues.</summary>
        TimeSpan AccessTokenLifetime { get; }
    }
}
