namespace MovieFlix.Api.DTOs.Auth
{
    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>UTC timestamp after which the access token is invalid.</summary>
        public DateTime AccessTokenExpiresAt { get; set; }

        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>UTC timestamp after which the refresh token is invalid.</summary>
        public DateTime RefreshTokenExpiresAt { get; set; }

        public string TokenType { get; set; } = "Bearer";

        public UserResponse User { get; set; } = new();
    }
}
