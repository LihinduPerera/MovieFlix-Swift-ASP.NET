using System.ComponentModel.DataAnnotations;

namespace MovieFlix.Api.DTOs.Ratings
{
    public class RateMovieRequest
    {
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; }
    }
}
