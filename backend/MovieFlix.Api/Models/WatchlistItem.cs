namespace MovieFlix.Api.Models;

/// <summary>A title the user saved to watch later.</summary>
public class WatchlistItem
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int MediaItemId { get; set; }

    public DateTime AddedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public MediaItem MediaItem { get; set; } = null!;
}
