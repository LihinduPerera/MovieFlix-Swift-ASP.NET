using MovieFlix.Api.DTOs.Media;

namespace MovieFlix.Api.DTOs.Reviews
{
    public class ReviewResponse
    {
        public int Id { get; set; }

        public MediaSummaryResponse Media { get; set; } = new();

        public string Content { get; set; } = string.Empty;

        public string AuthorDisplayName { get; set; } = string.Empty;

        public bool IsMine { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
