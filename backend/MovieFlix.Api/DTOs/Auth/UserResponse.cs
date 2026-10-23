namespace MovieFlix.Api.DTOs.Auth
{
    public class UserResponse
    {
        public string Id { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public List<string> Roles { get; set; } = [];
    }
}
