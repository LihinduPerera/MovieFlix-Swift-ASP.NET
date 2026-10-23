namespace MovieFlix.Api.DTOs.Reviews
{
    public class ReviewResponse
    {
        public int Id { get; set; }

        public int MovieId { get; set; }

        public int TmdbId { get; set; }

        public string MovieTitle { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string AuthorId { get; set; } = string.Empty;

        public string AuthorDisplayName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
