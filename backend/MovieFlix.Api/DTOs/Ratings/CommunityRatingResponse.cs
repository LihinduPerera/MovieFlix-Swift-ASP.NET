namespace MovieFlix.Api.DTOs.Ratings
{
    public class CommunityRatingResponse
    {
        public int TmdbId { get; set; }

        public string Title { get; set; } = string.Empty;

        /// <summary>Average of all user ratings, rounded to 2 decimals.</summary>
        public double AverageRating { get; set; }

        /// <summary>How many users have rated this movie.</summary>
        public int RatingCount { get; set; }

        /// <summary>The current user's own rating, when they have one.</summary>
        public int? UserRating { get; set; }
    }
}
