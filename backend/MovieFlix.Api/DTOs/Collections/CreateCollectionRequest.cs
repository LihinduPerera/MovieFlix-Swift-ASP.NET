using System.ComponentModel.DataAnnotations;

namespace MovieFlix.Api.DTOs.Collections
{
    public class CreateCollectionRequest
    {
        [Required]
        [MinLength(1)]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;
    }
}
