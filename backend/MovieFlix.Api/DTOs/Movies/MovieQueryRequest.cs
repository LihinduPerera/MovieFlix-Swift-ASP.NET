using System.ComponentModel.DataAnnotations;

namespace MovieFlix.Api.DTOs.Movies
{
    public class MovieQueryRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
        public int Page { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
        public int PageSize { get; set; } = 20;

        [MaxLength(200, ErrorMessage = "Search cannot exceed 200 characters.")]
        public string? Search { get; set; }

        public string SortBy { get; set; } = "id";

        public string SortOrder { get; set; } = "asc";
    }
}
