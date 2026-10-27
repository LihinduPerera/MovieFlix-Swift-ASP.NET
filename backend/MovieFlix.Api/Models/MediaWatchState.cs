namespace MovieFlix.Api.Models;

/// <summary>
/// Aggregate watch state for one user and one title. Movies use the progress
/// fields directly; TV shows additionally track the last watched episode.
/// </summary>
public class MediaWatchState
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int MediaItemId { get; set; }

    public int ProgressSeconds { get; set; }

    public bool Completed { get; set; }

    public DateTime LastWatchedAt { get; set; }

    public int? LastTvEpisodeId { get; set; }

    public int? LastSeasonNumber { get; set; }

    public int? LastEpisodeNumber { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public MediaItem MediaItem { get; set; } = null!;

    public TvEpisode? LastTvEpisode { get; set; }
}
