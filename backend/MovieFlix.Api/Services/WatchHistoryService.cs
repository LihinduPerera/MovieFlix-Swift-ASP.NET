using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.History;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Mappings;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>
    /// Aggregates watch state. Episode writes maintain both the episode state
    /// and the parent show's last/next episode pointers.
    /// </summary>
    public class WatchHistoryService : IWatchHistoryService
    {
        private readonly MovieFlixDbContext _context;
        private readonly IMediaCacheService _cacheService;
        private readonly ITvProgressService _tvProgressService;
        private readonly TimeProvider _timeProvider;
        private readonly TmdbSettings _settings;

        public WatchHistoryService(
            MovieFlixDbContext context,
            IMediaCacheService cacheService,
            ITvProgressService tvProgressService,
            TimeProvider timeProvider,
            IOptions<TmdbSettings> settings)
        {
            _context = context;
            _cacheService = cacheService;
            _tvProgressService = tvProgressService;
            _timeProvider = timeProvider;
            _settings = settings.Value;
        }

        public async Task<WatchHistoryItemResponse> RecordProgressAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            RecordProgressRequest request,
            CancellationToken cancellationToken = default)
        {
            if ((request.SeasonNumber is null) != (request.EpisodeNumber is null))
            {
                throw new RequestValidationException(
                    request.SeasonNumber is null ? "seasonNumber" : "episodeNumber",
                    "seasonNumber and episodeNumber must be supplied together.");
            }

            if (mediaType == MediaType.Movie
                && (request.SeasonNumber is not null || request.EpisodeNumber is not null))
            {
                throw new RequestValidationException(
                    "mediaType",
                    "Episode tracking is only supported for TV shows.");
            }

            if (mediaType == MediaType.Tv
                && request.SeasonNumber is not null
                && request.EpisodeNumber is not null)
            {
                var episodeResult = await RecordEpisodeProgressAsync(
                    userId,
                    tmdbId,
                    request.SeasonNumber.Value,
                    request.EpisodeNumber.Value,
                    new RecordEpisodeProgressRequest
                    {
                        ProgressSeconds = request.ProgressSeconds,
                        Completed = request.Completed
                    },
                    cancellationToken);

                var current = await _context.MediaWatchStates
                    .AsNoTracking()
                    .Include(state => state.MediaItem)
                    .FirstAsync(state => state.UserId == userId
                        && state.MediaItem.TmdbId == tmdbId
                        && state.MediaItem.MediaType == MediaType.Tv,
                        cancellationToken);

                return await BuildHistoryItemAsync(
                    current.MediaItem,
                    current,
                    cancellationToken);
            }

            var media = await _cacheService.GetOrCreateAsync(
                mediaType,
                tmdbId,
                _settings.DefaultLanguage,
                cancellationToken);

            var state = await UpsertMediaStateAsync(
                userId,
                media,
                request.ProgressSeconds,
                request.Completed,
                lastEpisodeId: null,
                lastSeasonNumber: null,
                lastEpisodeNumber: null,
                preserveLastEpisode: mediaType == MediaType.Tv,
                cancellationToken);

            return await BuildHistoryItemAsync(media, state, cancellationToken);
        }

        public async Task<EpisodeProgressResponse> RecordEpisodeProgressAsync(
            string userId,
            int tmdbId,
            int seasonNumber,
            int episodeNumber,
            RecordEpisodeProgressRequest request,
            CancellationToken cancellationToken = default)
        {
            if (seasonNumber < 1)
            {
                throw new RequestValidationException(
                    "seasonNumber",
                    "Season 0 specials cannot be tracked.");
            }

            var media = await _cacheService.GetOrCreateAsync(
                MediaType.Tv,
                tmdbId,
                _settings.DefaultLanguage,
                cancellationToken);

            var episode = await _cacheService.GetOrCreateEpisodeAsync(
                media.Id,
                seasonNumber,
                episodeNumber,
                _settings.DefaultLanguage,
                cancellationToken)
                ?? throw new ResourceNotFoundException("Episode not found.");

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var runtimeSeconds = episode.RuntimeMinutes is > 0
                ? episode.RuntimeMinutes.Value * 60
                : 0;
            var progressSeconds = request.Completed && runtimeSeconds > 0
                ? runtimeSeconds
                : request.ProgressSeconds;

            var episodeState = await UpsertEpisodeStateAsync(
                userId,
                episode.Id,
                progressSeconds,
                request.Completed,
                now,
                cancellationToken);

            var next = await _tvProgressService.GetNextEpisodeAsync(
                userId,
                media.Id,
                cancellationToken);

            // If the season just finished, load the following season so the
            // next episode is accurate across season boundaries.
            if (request.Completed && next is null)
            {
                var nextSeason = await _context.TvSeasons
                    .AsNoTracking()
                    .Where(season => season.MediaItemId == media.Id
                        && season.SeasonNumber > seasonNumber
                        && season.EpisodeCount > 0)
                    .OrderBy(season => season.SeasonNumber)
                    .FirstOrDefaultAsync(cancellationToken);

                if (nextSeason is not null)
                {
                    await _cacheService.EnsureSeasonEpisodesAsync(
                        media.Id,
                        nextSeason.SeasonNumber,
                        _settings.DefaultLanguage,
                        cancellationToken);

                    next = await _tvProgressService.GetNextEpisodeAsync(
                        userId,
                        media.Id,
                        cancellationToken);
                }
            }

            var showCompleted = request.Completed
                && next is null
                && await IsShowCompletedAsync(media.Id, cancellationToken);

            var state = await UpsertMediaStateAsync(
                userId,
                media,
                progressSeconds,
                showCompleted,
                episode.Id,
                seasonNumber,
                episodeNumber,
                preserveLastEpisode: false,
                cancellationToken);

            state.LastTvEpisodeId = episode.Id;
            state.LastSeasonNumber = seasonNumber;
            state.LastEpisodeNumber = episodeNumber;
            await _context.SaveChangesAsync(cancellationToken);

            return new EpisodeProgressResponse
            {
                Media = MediaMappings.ToSummary(media),
                Episode = MediaMappings.ToEpisodeResponse(
                    episode,
                    episodeState.ProgressSeconds,
                    episodeState.Completed),
                NextEpisode = next is null
                    ? null
                    : MediaMappings.ToEpisodeResponse(next),
                ShowCompleted = showCompleted
            };
        }

        public async Task<PagedResponse<WatchHistoryItemResponse>> GetHistoryAsync(
            string userId,
            MediaType? mediaType,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.MediaWatchStates
                .AsNoTracking()
                .Include(state => state.MediaItem)
                .Where(state => state.UserId == userId);

            if (mediaType is not null)
            {
                query = query.Where(state =>
                    state.MediaItem.MediaType == mediaType.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var states = await query
                .OrderByDescending(state => state.LastWatchedAt)
                .ThenByDescending(state => state.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var items = await BuildHistoryItemsAsync(userId, states, cancellationToken);

            return new PagedResponse<WatchHistoryItemResponse>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<List<WatchHistoryItemResponse>> GetContinueWatchingAsync(
            string userId,
            int limit,
            CancellationToken cancellationToken = default)
        {
            limit = Math.Clamp(limit, 1, 50);

            var states = await _context.MediaWatchStates
                .AsNoTracking()
                .Include(state => state.MediaItem)
                .Where(state => state.UserId == userId
                    && !state.Completed
                    && (state.MediaItem.MediaType == MediaType.Movie
                        ? state.ProgressSeconds > 0
                        : state.ProgressSeconds > 0 || state.LastTvEpisodeId != null))
                .OrderByDescending(state => state.LastWatchedAt)
                .Take(200)
                .ToListAsync(cancellationToken);

            var items = await BuildHistoryItemsAsync(userId, states, cancellationToken);

            return items.Take(limit).ToList();
        }

        public async Task<TvHistoryResponse?> GetTvHistoryAsync(
            string userId,
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            var media = await _context.MediaItems
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.MediaType == MediaType.Tv && item.TmdbId == tmdbId,
                    cancellationToken);

            if (media is null)
            {
                return null;
            }

            var states = await _context.EpisodeWatchStates
                .AsNoTracking()
                .Include(state => state.TvEpisode)
                .Where(state => state.UserId == userId
                    && state.TvEpisode.MediaItemId == media.Id)
                .OrderBy(state => state.TvEpisode.SeasonNumber)
                .ThenBy(state => state.TvEpisode.EpisodeNumber)
                .ToListAsync(cancellationToken);

            var next = await _tvProgressService.GetNextEpisodeAsync(
                userId,
                media.Id,
                cancellationToken);

            var hasEpisodes = await _context.TvEpisodes
                .AsNoTracking()
                .AnyAsync(episode => episode.MediaItemId == media.Id
                    && episode.SeasonNumber > 0,
                    cancellationToken);

            return new TvHistoryResponse
            {
                Media = MediaMappings.ToSummary(media),
                Episodes = states
                    .Select(state => new EpisodeWatchStateResponse
                    {
                        Episode = MediaMappings.ToEpisodeResponse(
                            state.TvEpisode,
                            state.ProgressSeconds,
                            state.Completed),
                        WatchedAt = state.WatchedAt
                    })
                    .ToList(),
                NextEpisode = next is null
                    ? null
                    : MediaMappings.ToEpisodeResponse(next),
                ShowCompleted = hasEpisodes && next is null
                    && await IsShowCompletedAsync(media.Id, cancellationToken)
            };
        }

        public async Task RemoveAsync(
            string userId,
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            var state = await _context.MediaWatchStates
                .FirstOrDefaultAsync(item => item.UserId == userId
                    && item.MediaItem.MediaType == mediaType
                    && item.MediaItem.TmdbId == tmdbId,
                    cancellationToken);

            if (state is null)
            {
                return;
            }

            var episodeStates = await _context.EpisodeWatchStates
                .Where(item => item.UserId == userId
                    && item.TvEpisode.MediaItemId == state.MediaItemId)
                .ToListAsync(cancellationToken);

            _context.EpisodeWatchStates.RemoveRange(episodeStates);
            _context.MediaWatchStates.Remove(state);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task ClearAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            var mediaStates = await _context.MediaWatchStates
                .Where(item => item.UserId == userId)
                .ToListAsync(cancellationToken);

            var episodeStates = await _context.EpisodeWatchStates
                .Where(item => item.UserId == userId)
                .ToListAsync(cancellationToken);

            _context.EpisodeWatchStates.RemoveRange(episodeStates);
            _context.MediaWatchStates.RemoveRange(mediaStates);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<List<WatchHistoryItemResponse>> BuildHistoryItemsAsync(
            string userId,
            List<MediaWatchState> states,
            CancellationToken cancellationToken)
        {
            if (states.Count == 0)
            {
                return [];
            }

            var tvMediaIds = states
                .Where(state => state.MediaItem.MediaType == MediaType.Tv)
                .Select(state => state.MediaItemId)
                .Distinct()
                .ToList();

            var nextMap = await _tvProgressService.GetNextEpisodesAsync(
                userId,
                tvMediaIds,
                cancellationToken);

            var lastEpisodeIds = states
                .Where(state => state.LastTvEpisodeId is not null)
                .Select(state => state.LastTvEpisodeId!.Value)
                .Distinct()
                .ToList();

            var lastEpisodes = lastEpisodeIds.Count == 0
                ? []
                : await _context.TvEpisodes
                    .AsNoTracking()
                    .Where(episode => lastEpisodeIds.Contains(episode.Id))
                    .ToDictionaryAsync(episode => episode.Id, cancellationToken);

            return states
                .Select(state =>
                {
                    TvEpisode? next = null;
                    TvEpisode? last = null;

                    if (state.MediaItem.MediaType == MediaType.Tv)
                    {
                        nextMap.TryGetValue(state.MediaItemId, out next);

                        if (state.LastTvEpisodeId is not null)
                        {
                            lastEpisodes.TryGetValue(state.LastTvEpisodeId.Value, out last);
                        }
                    }

                    return new WatchHistoryItemResponse
                    {
                        Media = MediaMappings.ToSummary(state.MediaItem),
                        ProgressSeconds = state.ProgressSeconds,
                        Completed = state.Completed,
                        LastWatchedAt = state.LastWatchedAt,
                        LastEpisode = last is null
                            ? null
                            : MediaMappings.ToEpisodeSummary(last),
                        NextEpisode = next is null
                            ? null
                            : MediaMappings.ToEpisodeSummary(next)
                    };
                })
                .ToList();
        }

        private async Task<WatchHistoryItemResponse> BuildHistoryItemAsync(
            MediaItem media,
            MediaWatchState state,
            CancellationToken cancellationToken)
        {
            TvEpisode? last = null;
            TvEpisode? next = null;

            if (media.MediaType == MediaType.Tv)
            {
                last = await _tvProgressService.GetEpisodeAsync(
                    state.LastTvEpisodeId,
                    cancellationToken);

                next = await _tvProgressService.GetNextEpisodeAsync(
                    state.UserId,
                    media.Id,
                    cancellationToken);
            }

            return new WatchHistoryItemResponse
            {
                Media = MediaMappings.ToSummary(media),
                ProgressSeconds = state.ProgressSeconds,
                Completed = state.Completed,
                LastWatchedAt = state.LastWatchedAt,
                LastEpisode = last is null
                    ? null
                    : MediaMappings.ToEpisodeSummary(last),
                NextEpisode = next is null
                    ? null
                    : MediaMappings.ToEpisodeSummary(next)
            };
        }

        private async Task<MediaWatchState> UpsertMediaStateAsync(
            string userId,
            MediaItem media,
            int progressSeconds,
            bool completed,
            int? lastEpisodeId,
            int? lastSeasonNumber,
            int? lastEpisodeNumber,
            bool preserveLastEpisode,
            CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var state = await _context.MediaWatchStates
                .FirstOrDefaultAsync(item => item.UserId == userId
                    && item.MediaItemId == media.Id,
                    cancellationToken);

            if (state is null)
            {
                state = new MediaWatchState
                {
                    UserId = userId,
                    MediaItemId = media.Id,
                    LastWatchedAt = now
                };

                _context.MediaWatchStates.Add(state);
            }

            state.ProgressSeconds = progressSeconds;
            state.Completed = completed;
            state.LastWatchedAt = now;

            if (!preserveLastEpisode)
            {
                state.LastTvEpisodeId = lastEpisodeId;
                state.LastSeasonNumber = lastSeasonNumber;
                state.LastEpisodeNumber = lastEpisodeNumber;
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();

                state = await _context.MediaWatchStates
                    .FirstAsync(item => item.UserId == userId
                        && item.MediaItemId == media.Id,
                        cancellationToken);

                state.ProgressSeconds = progressSeconds;
                state.Completed = completed;
                state.LastWatchedAt = now;

                if (!preserveLastEpisode)
                {
                    state.LastTvEpisodeId = lastEpisodeId;
                    state.LastSeasonNumber = lastSeasonNumber;
                    state.LastEpisodeNumber = lastEpisodeNumber;
                }

                await _context.SaveChangesAsync(cancellationToken);
            }

            return state;
        }

        private async Task<EpisodeWatchState> UpsertEpisodeStateAsync(
            string userId,
            int episodeId,
            int progressSeconds,
            bool completed,
            DateTime watchedAt,
            CancellationToken cancellationToken)
        {
            var state = await _context.EpisodeWatchStates
                .FirstOrDefaultAsync(item => item.UserId == userId
                    && item.TvEpisodeId == episodeId,
                    cancellationToken);

            if (state is null)
            {
                state = new EpisodeWatchState
                {
                    UserId = userId,
                    TvEpisodeId = episodeId
                };

                _context.EpisodeWatchStates.Add(state);
            }

            state.ProgressSeconds = progressSeconds;
            state.Completed = completed;
            state.WatchedAt = watchedAt;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();

                state = await _context.EpisodeWatchStates
                    .FirstAsync(item => item.UserId == userId
                        && item.TvEpisodeId == episodeId,
                        cancellationToken);

                state.ProgressSeconds = progressSeconds;
                state.Completed = completed;
                state.WatchedAt = watchedAt;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return state;
        }

        private async Task<bool> IsShowCompletedAsync(
            int mediaItemId,
            CancellationToken cancellationToken)
        {
            var hasUncachedSeasons = await _context.TvSeasons
                .AsNoTracking()
                .AnyAsync(season => season.MediaItemId == mediaItemId
                    && season.SeasonNumber > 0
                    && !_context.TvEpisodes.Any(episode =>
                        episode.TvSeasonId == season.Id),
                    cancellationToken);

            if (hasUncachedSeasons)
            {
                return false;
            }

            var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

            var hasFutureEpisodes = await _context.TvEpisodes
                .AsNoTracking()
                .AnyAsync(episode => episode.MediaItemId == mediaItemId
                    && episode.SeasonNumber > 0
                    && episode.AirDate > today,
                    cancellationToken);

            return !hasFutureEpisodes;
        }
    }
}
