namespace MovieFlix.Api.Models;

/// <summary>A cached season of a TV show. Season 0 holds TMDB specials.</summary>
public class TvSeason
{
    public int Id { get; set; }

    public int MediaItemId { get; set; }

    public int SeasonNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Overview { get; set; }

    public string? PosterPath { get; set; }

    public DateOnly? AirDate { get; set; }

    public int EpisodeCount { get; set; }

    public MediaItem MediaItem { get; set; } = null!;

    public ICollection<TvEpisode> Episodes { get; set; } = [];
}
