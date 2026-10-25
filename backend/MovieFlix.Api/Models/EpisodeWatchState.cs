namespace MovieFlix.Api.Models;

/// <summary>Per-episode watch state for a user.</summary>
public class EpisodeWatchState
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int TvEpisodeId { get; set; }

    public int ProgressSeconds { get; set; }

    public bool Completed { get; set; }

    public DateTime WatchedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public TvEpisode TvEpisode { get; set; } = null!;
}
