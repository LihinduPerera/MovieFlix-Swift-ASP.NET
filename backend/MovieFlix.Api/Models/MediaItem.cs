namespace MovieFlix.Api.Models;

/// <summary>
/// A single TMDB title (movie or TV show) cached locally. User-linked records
/// always reference this row; the public API identifies it by
/// <see cref="TmdbId"/> + <see cref="MediaType"/> instead of <see cref="Id"/>.
/// </summary>
public class MediaItem
{
    public int Id { get; set; }

    public int TmdbId { get; set; }

    public MediaType MediaType { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OriginalTitle { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string? PosterPath { get; set; }

    public string? BackdropPath { get; set; }

    /// <summary>Movie release date; for TV this is the first air date.</summary>
    public DateOnly? ReleaseDate { get; set; }

    public DateOnly? FirstAirDate { get; set; }

    public DateOnly? LastAirDate { get; set; }

    public double VoteAverage { get; set; }

    public int VoteCount { get; set; }

    public double Popularity { get; set; }

    public string OriginalLanguage { get; set; } = string.Empty;

    public bool Adult { get; set; }

    public string? Status { get; set; }

    public int? RuntimeMinutes { get; set; }

    public int? NumberOfSeasons { get; set; }

    public int? NumberOfEpisodes { get; set; }

    public string? Tagline { get; set; }

    public string? Homepage { get; set; }

    /// <summary>UTC timestamp of the last metadata refresh.</summary>
    public DateTime CachedAt { get; set; }

    public ICollection<MediaItemGenre> Genres { get; set; } = [];

    public ICollection<TvSeason> Seasons { get; set; } = [];
}
