namespace MovieFlix.Api.Models;

public class WatchlistItem
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int MovieId { get; set; }

    public DateTime AddedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Movie Movie { get; set; } = null!;
}
