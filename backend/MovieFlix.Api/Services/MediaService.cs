using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.DTOs.Tmdb;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Mappings;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>
    /// Discovery and details orchestration. TMDB list results are transient;
    /// details and TV metadata are persisted through <see cref="IMediaCacheService"/>.
    /// </summary>
    public class MediaService : IMediaService
    {
        private const int TmdbPageSize = 20;

        private readonly ITmdbService _tmdbService;
        private readonly IMediaCacheService _cacheService;
        private readonly MovieFlixDbContext _context;
        private readonly ITvProgressService _tvProgressService;
        private readonly TmdbSettings _settings;

        public MediaService(
            ITmdbService tmdbService,
            IMediaCacheService cacheService,
            MovieFlixDbContext context,
            ITvProgressService tvProgressService,
            IOptions<TmdbSettings> settings)
        {
            _tmdbService = tmdbService;
            _cacheService = cacheService;
            _context = context;
            _tvProgressService = tvProgressService;
            _settings = settings.Value;
        }

        public async Task<PagedResponse<MediaSummaryResponse>> SearchAsync(
            SearchQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            var language = ResolveLanguage(request.Language);
            var region = ResolveRegion(request.Region);

            var response = await _tmdbService.SearchAsync(
                request.Query.Trim(),
                request.Type,
                request.Page,
                language,
                region,
                cancellationToken);

            var items = MapResults(response.Results, request.Type);

            return ToPage(response, items, request.Page);
        }

        public async Task<PagedResponse<MediaSummaryResponse>> GetTrendingAsync(
            TrendingQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _tmdbService.GetTrendingAsync(
                request.MediaType,
                request.Window,
                request.Page,
                ResolveLanguage(request.Language),
                cancellationToken);

            var defaultType = request.MediaType == MediaTypeFilter.Movie
                ? MediaType.Movie
                : MediaType.Tv;

            var items = response.Results
                .Select(result =>
                {
                    var type = request.MediaType == MediaTypeFilter.All
                        ? MediaMappings.ParseMediaType(result.MediaType)
                        : defaultType;

                    return type is null
                        ? null
                        : MediaMappings.ToSummary(result, type.Value);
                })
                .Where(item => item is not null)
                .Select(item => item!)
                .ToList();

            return ToPage(response, items, request.Page);
        }

        public async Task<PagedResponse<MediaSummaryResponse>> GetFeedAsync(
            MediaType mediaType,
            MediaFeed feed,
            MediaListQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _tmdbService.GetFeedAsync(
                mediaType,
                feed,
                request.Page,
                ResolveLanguage(request.Language),
                ResolveRegion(request.Region),
                cancellationToken);

            var items = response.Results
                .Select(result => MediaMappings.ToSummary(result, mediaType))
                .ToList();

            return ToPage(response, items, request.Page);
        }

        public async Task<PagedResponse<MediaSummaryResponse>> DiscoverAsync(
            MediaType mediaType,
            DiscoverQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _tmdbService.GetDiscoverAsync(
                mediaType,
                request.GenreId,
                request.Year,
                request.MinRating,
                request.SortBy,
                request.Page,
                ResolveLanguage(request.Language),
                ResolveRegion(request.Region),
                cancellationToken);

            var items = response.Results
                .Select(result => MediaMappings.ToSummary(result, mediaType))
                .ToList();

            return ToPage(response, items, request.Page);
        }

        public async Task<MediaDetailsResponse?> GetDetailsAsync(
            MediaType mediaType,
            int tmdbId,
            string? language,
            CancellationToken cancellationToken = default)
        {
            var details = await _tmdbService.GetDetailsAsync(
                mediaType,
                tmdbId,
                ResolveLanguage(language),
                cancellationToken);

            var media = await _cacheService.UpsertAsync(
                mediaType,
                details,
                cancellationToken);

            return MapDetails(media, details);
        }

        public async Task<List<SeasonSummaryResponse>> GetSeasonsAsync(
            int tmdbId,
            string? language,
            CancellationToken cancellationToken = default)
        {
            var media = await _cacheService.GetOrCreateAsync(
                MediaType.Tv,
                tmdbId,
                ResolveLanguage(language),
                cancellationToken);

            return await _context.TvSeasons
                .AsNoTracking()
                .Where(season => season.MediaItemId == media.Id && season.SeasonNumber > 0)
                .OrderBy(season => season.SeasonNumber)
                .Select(season => new SeasonSummaryResponse
                {
                    SeasonNumber = season.SeasonNumber,
                    Name = season.Name,
                    Overview = season.Overview,
                    PosterPath = season.PosterPath,
                    AirDate = season.AirDate,
                    EpisodeCount = season.EpisodeCount
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<SeasonDetailsResponse?> GetSeasonDetailsAsync(
            int tmdbId,
            int seasonNumber,
            string? language,
            string? userId,
            CancellationToken cancellationToken = default)
        {
            var media = await _cacheService.GetOrCreateAsync(
                MediaType.Tv,
                tmdbId,
                ResolveLanguage(language),
                cancellationToken);

            await _cacheService.EnsureSeasonEpisodesAsync(
                media.Id,
                seasonNumber,
                ResolveLanguage(language),
                cancellationToken);

            var season = await _context.TvSeasons
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.MediaItemId == media.Id
                        && item.SeasonNumber == seasonNumber,
                    cancellationToken);

            if (season is null)
            {
                return null;
            }

            var episodes = await _context.TvEpisodes
                .AsNoTracking()
                .Where(episode => episode.TvSeasonId == season.Id)
                .OrderBy(episode => episode.EpisodeNumber)
                .ToListAsync(cancellationToken);

            var states = new Dictionary<int, EpisodeWatchState>();

            if (!string.IsNullOrWhiteSpace(userId) && episodes.Count > 0)
            {
                var ids = episodes.Select(episode => episode.Id).ToList();

                states = await _context.EpisodeWatchStates
                    .AsNoTracking()
                    .Where(state => state.UserId == userId
                        && ids.Contains(state.TvEpisodeId))
                    .ToDictionaryAsync(state => state.TvEpisodeId, cancellationToken);
            }

            return new SeasonDetailsResponse
            {
                SeasonNumber = season.SeasonNumber,
                Name = season.Name,
                Overview = season.Overview,
                PosterPath = season.PosterPath,
                AirDate = season.AirDate,
                Episodes = episodes
                    .Select(episode =>
                    {
                        states.TryGetValue(episode.Id, out var state);

                        return MediaMappings.ToEpisodeResponse(
                            episode,
                            state?.ProgressSeconds ?? 0,
                            state?.Completed ?? false);
                    })
                    .ToList()
            };
        }

        public async Task<MediaStateResponse> GetStateAsync(
            MediaType mediaType,
            int tmdbId,
            string? userId,
            CancellationToken cancellationToken = default)
        {
            var response = new MediaStateResponse
            {
                TmdbId = tmdbId,
                MediaType = mediaType
            };

            if (string.IsNullOrWhiteSpace(userId))
            {
                return response;
            }

            var media = await _context.MediaItems
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.MediaType == mediaType && item.TmdbId == tmdbId,
                    cancellationToken);

            if (media is null)
            {
                return response;
            }

            response.IsFavorite = await _context.Favorites
                .AsNoTracking()
                .AnyAsync(
                    favorite => favorite.UserId == userId
                        && favorite.MediaItemId == media.Id,
                    cancellationToken);

            response.IsInWatchlist = await _context.WatchlistItems
                .AsNoTracking()
                .AnyAsync(
                    item => item.UserId == userId && item.MediaItemId == media.Id,
                    cancellationToken);

            response.UserRating = await _context.MediaRatings
                .AsNoTracking()
                .Where(rating => rating.UserId == userId
                    && rating.MediaItemId == media.Id)
                .Select(rating => (int?)rating.Rating)
                .FirstOrDefaultAsync(cancellationToken);

            var review = await _context.Reviews
                .AsNoTracking()
                .Where(item => item.UserId == userId
                    && item.MediaItemId == media.Id)
                .Select(item => new { item.Id })
                .FirstOrDefaultAsync(cancellationToken);

            response.HasReview = review is not null;
            response.ReviewId = review?.Id;

            var state = await _context.MediaWatchStates
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.UserId == userId && item.MediaItemId == media.Id,
                    cancellationToken);

            if (state is not null)
            {
                response.ProgressSeconds = state.ProgressSeconds;
                response.Completed = state.Completed;

                if (state.LastTvEpisodeId is not null)
                {
                    var lastEpisode = await _tvProgressService.GetEpisodeAsync(
                        state.LastTvEpisodeId,
                        cancellationToken);

                    response.LastEpisode = lastEpisode is null
                        ? null
                        : MediaMappings.ToEpisodeSummary(lastEpisode);
                }
            }

            if (mediaType == MediaType.Tv)
            {
                var next = await _tvProgressService.GetNextEpisodeAsync(
                    userId,
                    media.Id,
                    cancellationToken);

                response.NextEpisode = next is null
                    ? null
                    : MediaMappings.ToEpisodeSummary(next);
            }

            return response;
        }

        public async Task<List<GenreResponse>> GetGenresAsync(
            MediaType mediaType,
            string? language,
            CancellationToken cancellationToken = default)
        {
            var genres = await _tmdbService.GetGenresAsync(
                mediaType,
                ResolveLanguage(language),
                cancellationToken);

            return genres
                .Select(genre => new GenreResponse
                {
                    Id = genre.Id,
                    Name = genre.Name
                })
                .ToList();
        }

        private static List<MediaSummaryResponse> MapResults(
            IEnumerable<TmdbSearchResult> results,
            SearchType searchType)
        {
            var mapped = new List<MediaSummaryResponse>();

            foreach (var result in results)
            {
                var type = searchType switch
                {
                    SearchType.Movie => MediaType.Movie,
                    SearchType.Tv => MediaType.Tv,
                    _ => MediaMappings.ParseMediaType(result.MediaType)
                };

                if (type is null)
                {
                    continue;
                }

                mapped.Add(MediaMappings.ToSummary(result, type.Value));
            }

            return mapped;
        }

        private static PagedResponse<MediaSummaryResponse> ToPage(
            TmdbSearchResponse response,
            List<MediaSummaryResponse> items,
            int requestedPage)
        {
            return new PagedResponse<MediaSummaryResponse>
            {
                Items = items,
                Page = requestedPage,
                PageSize = TmdbPageSize,
                TotalCount = response.TotalResults,
                TotalPages = response.TotalPages
            };
        }

        private static MediaDetailsResponse MapDetails(
            MediaItem media,
            TmdbMediaDetailsResponse details)
        {
            var response = new MediaDetailsResponse
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
                Popularity = media.Popularity,
                Tagline = media.Tagline,
                OriginalLanguage = media.OriginalLanguage,
                Adult = media.Adult,
                Status = media.Status,
                RuntimeMinutes = media.RuntimeMinutes,
                NumberOfSeasons = media.NumberOfSeasons,
                NumberOfEpisodes = media.NumberOfEpisodes,
                FirstAirDate = media.FirstAirDate,
                LastAirDate = media.LastAirDate,
                Genres = details.Genres
                    .Select(genre => new GenreResponse
                    {
                        Id = genre.Id,
                        Name = genre.Name
                    })
                    .ToList(),
                Credits = new CreditsResponse
                {
                    Cast = details.Credits?.Cast
                        .OrderBy(member => member.Order)
                        .Select(member => new CastMemberResponse
                        {
                            Id = member.Id,
                            Name = member.Name,
                            Character = member.Character,
                            ProfilePath = member.ProfilePath,
                            Order = member.Order
                        })
                        .ToList() ?? [],
                    Crew = details.Credits?.Crew
                        .Select(member => new CrewMemberResponse
                        {
                            Id = member.Id,
                            Name = member.Name,
                            Job = member.Job ?? string.Empty,
                            Department = member.Department ?? string.Empty,
                            ProfilePath = member.ProfilePath
                        })
                        .ToList() ?? []
                },
                Videos = details.Videos?.Results
                    .Select(video => new VideoResponse
                    {
                        Id = video.Id,
                        Key = video.Key,
                        Name = video.Name,
                        Site = video.Site,
                        Type = video.Type,
                        Official = video.Official,
                        Language = video.Language,
                        PublishedAt = video.PublishedAt
                    })
                    .ToList() ?? [],
                Similar = details.Similar?.Results
                    .Where(result => result.MediaType is null
                        || MediaMappings.ParseMediaType(result.MediaType) == media.MediaType)
                    .Take(20)
                    .Select(result => MediaMappings.ToSummary(result, media.MediaType))
                    .ToList() ?? [],
                Seasons = details.Seasons
                    .Where(season => season.SeasonNumber > 0)
                    .OrderBy(season => season.SeasonNumber)
                    .Select(season => new SeasonSummaryResponse
                    {
                        SeasonNumber = season.SeasonNumber,
                        Name = season.Name,
                        Overview = season.Overview,
                        PosterPath = season.PosterPath,
                        AirDate = MediaMappings.ParseDate(season.AirDate),
                        EpisodeCount = season.EpisodeCount
                    })
                    .ToList()
            };

            return response;
        }

        private string ResolveLanguage(string? language) =>
            string.IsNullOrWhiteSpace(language)
                ? _settings.DefaultLanguage
                : language.Trim();

        private string ResolveRegion(string? region) =>
            string.IsNullOrWhiteSpace(region)
                ? _settings.DefaultRegion
                : region.Trim().ToUpperInvariant();
    }
}
