using System.Globalization;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.DTOs.Tmdb;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Mappings
{
    /// <summary>Pure mapping helpers shared by the media cache and API services.</summary>
    public static class MediaMappings
    {
        private const string DateFormat = "yyyy-MM-dd";

        public static MediaType? ParseMediaType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "movie" => MediaType.Movie,
                "tv" => MediaType.Tv,
                _ => null
            };
        }

        public static DateOnly? ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return DateOnly.TryParseExact(
                value,
                DateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date)
                ? date
                : null;
        }

        public static MediaSummaryResponse ToSummary(MediaItem media)
        {
            return new MediaSummaryResponse
            {
                TmdbId = media.TmdbId,
                MediaType = media.MediaType,
                Title = media.Title,
                OriginalTitle = media.OriginalTitle,
                Overview = media.Overview,
                PosterPath = media.PosterPath,
                BackdropPath = media.BackdropPath,
                ReleaseDate = media.ReleaseDate,
                VoteAverage = media.VoteAverage,
                VoteCount = media.VoteCount,
                Popularity = media.Popularity
            };
        }

        public static MediaSummaryResponse ToSummary(
            TmdbSearchResult result,
            MediaType mediaType)
        {
            var title = mediaType == MediaType.Movie
                ? result.Title ?? result.Name ?? string.Empty
                : result.Name ?? result.Title ?? string.Empty;

            var originalTitle = mediaType == MediaType.Movie
                ? result.OriginalTitle ?? result.OriginalName ?? title
                : result.OriginalName ?? result.OriginalTitle ?? title;

            var releaseDate = mediaType == MediaType.Movie
                ? ParseDate(result.ReleaseDate)
                : ParseDate(result.FirstAirDate);

            return new MediaSummaryResponse
            {
                TmdbId = result.Id,
                MediaType = mediaType,
                Title = title,
                OriginalTitle = originalTitle,
                Overview = result.Overview ?? string.Empty,
                PosterPath = result.PosterPath,
                BackdropPath = result.BackdropPath,
                ReleaseDate = releaseDate,
                VoteAverage = result.VoteAverage,
                VoteCount = result.VoteCount,
                Popularity = result.Popularity
            };
        }

        public static MediaItem ToMediaItem(
            MediaType mediaType,
            TmdbMediaDetailsResponse details,
            DateTime cachedAt)
        {
            var media = new MediaItem { MediaType = mediaType };

            ApplyDetails(media, details, cachedAt);

            return media;
        }

        public static void ApplyDetails(
            MediaItem media,
            TmdbMediaDetailsResponse details,
            DateTime cachedAt)
        {
            var title = media.MediaType == MediaType.Movie
                ? details.Title ?? details.Name ?? string.Empty
                : details.Name ?? details.Title ?? string.Empty;

            var originalTitle = media.MediaType == MediaType.Movie
                ? details.OriginalTitle ?? details.OriginalName ?? title
                : details.OriginalName ?? details.OriginalTitle ?? title;

            media.TmdbId = details.Id;
            media.Title = title;
            media.OriginalTitle = originalTitle;
            media.Overview = details.Overview ?? string.Empty;
            media.PosterPath = details.PosterPath;
            media.BackdropPath = details.BackdropPath;
            media.ReleaseDate = media.MediaType == MediaType.Movie
                ? ParseDate(details.ReleaseDate)
                : ParseDate(details.FirstAirDate);
            media.FirstAirDate = ParseDate(details.FirstAirDate);
            media.LastAirDate = ParseDate(details.LastAirDate);
            media.VoteAverage = details.VoteAverage;
            media.VoteCount = details.VoteCount;
            media.Popularity = details.Popularity;
            media.OriginalLanguage = details.OriginalLanguage ?? string.Empty;
            media.Adult = details.Adult;
            media.Status = details.Status;
            media.RuntimeMinutes = media.MediaType == MediaType.Movie
                ? details.Runtime
                : null;
            media.NumberOfSeasons = media.MediaType == MediaType.Tv
                ? details.NumberOfSeasons
                : null;
            media.NumberOfEpisodes = media.MediaType == MediaType.Tv
                ? details.NumberOfEpisodes
                : null;
            media.Tagline = details.Tagline;
            media.Homepage = details.Homepage;
            media.CachedAt = cachedAt;
        }

        public static SeasonSummaryResponse ToSeasonSummary(TvSeason season)
        {
            return new SeasonSummaryResponse
            {
                SeasonNumber = season.SeasonNumber,
                Name = season.Name,
                Overview = season.Overview,
                PosterPath = season.PosterPath,
                AirDate = season.AirDate,
                EpisodeCount = season.EpisodeCount
            };
        }

        public static EpisodeSummaryResponse ToEpisodeSummary(TvEpisode episode)
        {
            return new EpisodeSummaryResponse
            {
                SeasonNumber = episode.SeasonNumber,
                EpisodeNumber = episode.EpisodeNumber,
                Name = episode.Name,
                StillPath = episode.StillPath
            };
        }

        public static EpisodeResponse ToEpisodeResponse(
            TvEpisode episode,
            int progressSeconds = 0,
            bool completed = false)
        {
            return new EpisodeResponse
            {
                TmdbId = episode.TmdbId,
                SeasonNumber = episode.SeasonNumber,
                EpisodeNumber = episode.EpisodeNumber,
                Name = episode.Name,
                Overview = episode.Overview,
                StillPath = episode.StillPath,
                AirDate = episode.AirDate,
                RuntimeMinutes = episode.RuntimeMinutes,
                VoteAverage = episode.VoteAverage,
                VoteCount = episode.VoteCount,
                ProgressSeconds = progressSeconds,
                Completed = completed
            };
        }

        public static GenreResponse ToGenreResponse(Genre genre)
        {
            return new GenreResponse
            {
                Id = genre.TmdbId,
                Name = genre.Name
            };
        }
    }
}
