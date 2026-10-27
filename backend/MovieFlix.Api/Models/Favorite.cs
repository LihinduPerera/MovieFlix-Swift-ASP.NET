namespace MovieFlix.Api.Models;

/// <summary>A title the user marked as a favorite.</summary>
public class Favorite
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int MediaItemId { get; set; }

    public DateTime CreatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public MediaItem MediaItem { get; set; } = null!;
}
