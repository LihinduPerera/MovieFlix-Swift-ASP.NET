namespace MovieFlix.Api.Models;

/// <summary>A cached TV episode. Episodes are upserted, never deleted during refresh.</summary>
public class TvEpisode
{
    public int Id { get; set; }

    public int MediaItemId { get; set; }

    public int TvSeasonId { get; set; }

    public int? TmdbId { get; set; }

    public int SeasonNumber { get; set; }

    public int EpisodeNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Overview { get; set; }

    public string? StillPath { get; set; }

    public DateOnly? AirDate { get; set; }

    public int? RuntimeMinutes { get; set; }

    public double VoteAverage { get; set; }

    public int VoteCount { get; set; }

    public MediaItem MediaItem { get; set; } = null!;

    public TvSeason TvSeason { get; set; } = null!;
}
