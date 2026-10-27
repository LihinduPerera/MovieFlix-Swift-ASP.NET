using System.ComponentModel.DataAnnotations;
using MovieFlix.Api.DTOs.Media;

namespace MovieFlix.Api.DTOs.Ratings
{
    public class RatingRequest
    {
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; }
    }

    public class MediaRatingResponse
    {
        public MediaSummaryResponse Media { get; set; } = new();

        public int Rating { get; set; }

        public DateTime RatedAt { get; set; }
    }

    public class CommunityRatingResponse
    {
        public MediaSummaryResponse Media { get; set; } = new();

        public double AverageRating { get; set; }

        public int RatingCount { get; set; }

        public int? UserRating { get; set; }
    }
}
