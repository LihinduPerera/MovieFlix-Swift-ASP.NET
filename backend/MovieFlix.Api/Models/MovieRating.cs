namespace MovieFlix.Api.Models;

public class MovieRating
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int MovieId { get; set; }

    /// <summary>Rating value between 1 and 5 (inclusive).</summary>
    public int Rating { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Movie Movie { get; set; } = null!;
}
