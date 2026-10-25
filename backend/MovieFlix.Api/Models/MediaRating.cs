namespace MovieFlix.Api.Models;

/// <summary>A user's 1-5 star rating for a movie or TV show.</summary>
public class MediaRating
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int MediaItemId { get; set; }

    public int Rating { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public MediaItem MediaItem { get; set; } = null!;
}
