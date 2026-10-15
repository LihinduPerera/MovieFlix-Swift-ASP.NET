using System.ComponentModel.DataAnnotations;

namespace MovieFlix.Api.DTOs.SearchHistory
{
    public class RecordSearchRequest
    {
        [Required]
        [MinLength(1)]
        [MaxLength(200)]
        public string Query { get; set; } = string.Empty;
    }
}
