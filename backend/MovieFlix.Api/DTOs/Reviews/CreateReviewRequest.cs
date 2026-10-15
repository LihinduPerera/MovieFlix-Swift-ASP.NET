using System.ComponentModel.DataAnnotations;

namespace MovieFlix.Api.DTOs.Reviews
{
    public class CreateReviewRequest
    {
        [Required]
        [MinLength(1)]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;
    }
}
