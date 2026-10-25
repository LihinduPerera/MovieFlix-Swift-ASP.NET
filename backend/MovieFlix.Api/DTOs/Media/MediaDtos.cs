using MovieFlix.Api.Models;

namespace MovieFlix.Api.DTOs.Media
{
    /// <summary>Compact public representation used in every list and nested reference.</summary>
    public class MediaSummaryResponse
    {
        public int TmdbId { get; set; }

        public MediaType MediaType { get; set; }

        public string Title { get; set; } = string.Empty;

        public string OriginalTitle { get; set; } = string.Empty;

        public string Overview { get; set; } = string.Empty;

        public string? PosterPath { get; set; }

        public string? BackdropPath { get; set; }

        public DateOnly? ReleaseDate { get; set; }

        public double VoteAverage { get; set; }

        public int VoteCount { get; set; }

        public double Popularity { get; set; }
    }

    /// <summary>Full details for a movie or TV show.</summary>
    public class MediaDetailsResponse : MediaSummaryResponse
    {
        public string? Tagline { get; set; }

        public string OriginalLanguage { get; set; } = string.Empty;

        public bool Adult { get; set; }

        public string? Status { get; set; }

        public int? RuntimeMinutes { get; set; }

        public int? NumberOfSeasons { get; set; }

        public int? NumberOfEpisodes { get; set; }

        public DateOnly? FirstAirDate { get; set; }

        public DateOnly? LastAirDate { get; set; }

        public List<GenreResponse> Genres { get; set; } = [];

        public CreditsResponse Credits { get; set; } = new();

        public List<VideoResponse> Videos { get; set; } = [];

        public List<MediaSummaryResponse> Similar { get; set; } = [];

        public List<SeasonSummaryResponse> Seasons { get; set; } = [];
    }

    public class GenreResponse
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public class CreditsResponse
    {
        public List<CastMemberResponse> Cast { get; set; } = [];

        public List<CrewMemberResponse> Crew { get; set; } = [];
    }

    public class CastMemberResponse
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Character { get; set; }

        public string? ProfilePath { get; set; }

        public int Order { get; set; }
    }

    public class CrewMemberResponse
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Job { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public string? ProfilePath { get; set; }
    }

    public class VideoResponse
    {
        public string Id { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Site { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public bool Official { get; set; }

        public string? Language { get; set; }

        public DateTime? PublishedAt { get; set; }
    }

    public class SeasonSummaryResponse
    {
        public int SeasonNumber { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Overview { get; set; }

        public string? PosterPath { get; set; }

        public DateOnly? AirDate { get; set; }

        public int EpisodeCount { get; set; }
    }

    public class SeasonDetailsResponse
    {
        public int SeasonNumber { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Overview { get; set; }

        public string? PosterPath { get; set; }

        public DateOnly? AirDate { get; set; }

        public List<EpisodeResponse> Episodes { get; set; } = [];
    }

    public class EpisodeSummaryResponse
    {
        public int SeasonNumber { get; set; }

        public int EpisodeNumber { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? StillPath { get; set; }
    }

    public class EpisodeResponse : EpisodeSummaryResponse
    {
        public int? TmdbId { get; set; }

        public string? Overview { get; set; }

        public DateOnly? AirDate { get; set; }

        public int? RuntimeMinutes { get; set; }

        public double VoteAverage { get; set; }

        public int VoteCount { get; set; }

        public int ProgressSeconds { get; set; }

        public bool Completed { get; set; }
    }

    /// <summary>User state for a title. Anonymous callers receive safe defaults.</summary>
    public class MediaStateResponse
    {
        public int TmdbId { get; set; }

        public MediaType MediaType { get; set; }

        public bool IsFavorite { get; set; }

        public bool IsInWatchlist { get; set; }

        public int? UserRating { get; set; }

        public int? ReviewId { get; set; }

        public bool HasReview { get; set; }

        public int ProgressSeconds { get; set; }

        public bool Completed { get; set; }

        public EpisodeSummaryResponse? LastEpisode { get; set; }

        public EpisodeSummaryResponse? NextEpisode { get; set; }
    }

    public class SavedMediaResponse
    {
        public MediaSummaryResponse Media { get; set; } = new();

        public DateTime SavedAt { get; set; }
    }

    public class WatchHistoryItemResponse
    {
        public MediaSummaryResponse Media { get; set; } = new();

        public int ProgressSeconds { get; set; }

        public bool Completed { get; set; }

        public DateTime LastWatchedAt { get; set; }

        public EpisodeSummaryResponse? LastEpisode { get; set; }

        public EpisodeSummaryResponse? NextEpisode { get; set; }
    }

    public class EpisodeProgressResponse
    {
        public MediaSummaryResponse Media { get; set; } = new();

        public EpisodeResponse Episode { get; set; } = new();

        public EpisodeResponse? NextEpisode { get; set; }

        public bool ShowCompleted { get; set; }
    }

    public class EpisodeWatchStateResponse
    {
        public EpisodeResponse Episode { get; set; } = new();

        public DateTime WatchedAt { get; set; }
    }

    public class TvHistoryResponse
    {
        public MediaSummaryResponse Media { get; set; } = new();

        public List<EpisodeWatchStateResponse> Episodes { get; set; } = [];

        public EpisodeResponse? NextEpisode { get; set; }

        public bool ShowCompleted { get; set; }
    }
}
