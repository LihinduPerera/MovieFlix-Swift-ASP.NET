namespace MovieFlix.Api.DTOs.Ratings
{
    public class UserRatingResponse
    {
        public int MovieId { get; set; }

        public int TmdbId { get; set; }

        public string Title { get; set; } = string.Empty;

        /// <summary>The current user's rating (1–5).</summary>
        public int Rating { get; set; }

        public DateTime RatedAt { get; set; }
    }
}
