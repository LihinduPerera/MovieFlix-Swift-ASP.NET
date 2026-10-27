namespace MovieFlix.Api.Models;

/// <summary>A user's review of a movie or TV show (one per user per title).</summary>
public class Review
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int MediaItemId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public MediaItem MediaItem { get; set; } = null!;
}
