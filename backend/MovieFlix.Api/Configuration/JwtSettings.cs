namespace MovieFlix.Api.Configuration
{
    public class JwtSettings
    {
        public string Issuer { get; set; } = string.Empty;

        public string Audience { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        /// <summary>Lifetime of the short-lived access token, in minutes.</summary>
        public int AccessTokenMinutes { get; set; } = 60;

        /// <summary>Lifetime of the long-lived refresh token, in days.</summary>
        public int RefreshTokenDays { get; set; } = 7;
    }
}
