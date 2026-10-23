using System.ComponentModel.DataAnnotations;

namespace MovieFlix.Api.DTOs.Auth
{
    public class RefreshRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
