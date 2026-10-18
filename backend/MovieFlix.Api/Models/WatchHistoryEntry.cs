namespace MovieFlix.Api.Models;

public class WatchHistoryEntry
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int MovieId { get; set; }

    /// <summary>When the movie was last watched (updated on every progress write).</summary>
    public DateTime WatchedAt { get; set; }

    /// <summary>Playback position in seconds.</summary>
    public int ProgressSeconds { get; set; }

    public bool Completed { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Movie Movie { get; set; } = null!;
}
