using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>
    /// Resolves the "next episode" for a user from cached episode metadata.
    /// Never calls TMDB: callers that need episodes must cache them first.
    /// Season 0 specials are ignored.
    /// </summary>
    public interface ITvProgressService
    {
        Task<TvEpisode?> GetNextEpisodeAsync(
            string userId,
            int mediaItemId,
            CancellationToken cancellationToken = default);

        Task<Dictionary<int, TvEpisode?>> GetNextEpisodesAsync(
            string userId,
            IReadOnlyCollection<int> mediaItemIds,
            CancellationToken cancellationToken = default);

        Task<TvEpisode?> GetEpisodeAsync(
            int? episodeId,
            CancellationToken cancellationToken = default);
    }

    public class TvProgressService : ITvProgressService
    {
        private readonly MovieFlixDbContext _context;
        private readonly TimeProvider _timeProvider;

        public TvProgressService(
            MovieFlixDbContext context,
            TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<TvEpisode?> GetNextEpisodeAsync(
            string userId,
            int mediaItemId,
            CancellationToken cancellationToken = default)
        {
            var map = await GetNextEpisodesAsync(
                userId,
                [mediaItemId],
                cancellationToken);

            return map.GetValueOrDefault(mediaItemId);
        }

        public async Task<Dictionary<int, TvEpisode?>> GetNextEpisodesAsync(
            string userId,
            IReadOnlyCollection<int> mediaItemIds,
            CancellationToken cancellationToken = default)
        {
            var result = mediaItemIds.ToDictionary(id => id, _ => (TvEpisode?)null);

            if (mediaItemIds.Count == 0 || string.IsNullOrWhiteSpace(userId))
            {
                return result;
            }

            var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

            var episodes = await _context.TvEpisodes
                .AsNoTracking()
                .Where(episode => mediaItemIds.Contains(episode.MediaItemId)
                    && episode.SeasonNumber > 0
                    && (episode.AirDate == null || episode.AirDate <= today))
                .OrderBy(episode => episode.MediaItemId)
                .ThenBy(episode => episode.SeasonNumber)
                .ThenBy(episode => episode.EpisodeNumber)
                .Select(episode => new
                {
                    episode.Id,
                    episode.MediaItemId,
                    episode.SeasonNumber,
                    episode.EpisodeNumber
                })
                .ToListAsync(cancellationToken);

            if (episodes.Count == 0)
            {
                return result;
            }

            var episodeIds = episodes.Select(episode => episode.Id).ToList();

            var completedIds = (await _context.EpisodeWatchStates
                .AsNoTracking()
                .Where(state => state.UserId == userId
                    && state.Completed
                    && episodeIds.Contains(state.TvEpisodeId))
                .Select(state => state.TvEpisodeId)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            var nextIds = new List<int>();

            foreach (var group in episodes.GroupBy(episode => episode.MediaItemId))
            {
                var next = group.FirstOrDefault(episode => !completedIds.Contains(episode.Id));

                if (next is not null)
                {
                    result[group.Key] = null;
                    nextIds.Add(next.Id);
                }
            }

            if (nextIds.Count == 0)
            {
                return result;
            }

            var nextEpisodes = await _context.TvEpisodes
                .AsNoTracking()
                .Where(episode => nextIds.Contains(episode.Id))
                .ToListAsync(cancellationToken);

            foreach (var episode in nextEpisodes)
            {
                result[episode.MediaItemId] = episode;
            }

            return result;
        }

        public Task<TvEpisode?> GetEpisodeAsync(
            int? episodeId,
            CancellationToken cancellationToken = default)
        {
            if (episodeId is null)
            {
                return Task.FromResult<TvEpisode?>(null);
            }

            return _context.TvEpisodes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    episode => episode.Id == episodeId.Value,
                    cancellationToken);
        }
    }
}
