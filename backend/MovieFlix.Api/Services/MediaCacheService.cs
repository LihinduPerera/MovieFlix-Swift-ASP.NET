using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Tmdb;
using MovieFlix.Api.Mappings;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>
    /// SQLite/Postgres-backed metadata cache. Concurrent first requests for the
    /// same title are resolved by the unique (TmdbId, MediaType) index: the
    /// loser re-reads the winner's row instead of failing the request.
    /// </summary>
    public class MediaCacheService : IMediaCacheService
    {
        private readonly MovieFlixDbContext _context;
        private readonly ITmdbService _tmdbService;
        private readonly TimeProvider _timeProvider;

        public MediaCacheService(
            MovieFlixDbContext context,
            ITmdbService tmdbService,
            TimeProvider timeProvider)
        {
            _context = context;
            _tmdbService = tmdbService;
            _timeProvider = timeProvider;
        }

        public async Task<MediaItem> GetOrCreateAsync(
            MediaType mediaType,
            int tmdbId,
            string language,
            CancellationToken cancellationToken = default)
        {
            var cached = await _context.MediaItems
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    media => media.MediaType == mediaType && media.TmdbId == tmdbId,
                    cancellationToken);

            if (cached is not null)
            {
                return cached;
            }

            var details = await _tmdbService.GetDetailsAsync(
                mediaType,
                tmdbId,
                language,
                cancellationToken);

            return await UpsertAsync(mediaType, details, cancellationToken);
        }

        public async Task<MediaItem> UpsertAsync(
            MediaType mediaType,
            TmdbMediaDetailsResponse details,
            CancellationToken cancellationToken = default)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var media = await _context.MediaItems
                .Include(item => item.Genres)
                .ThenInclude(link => link.Genre)
                .FirstOrDefaultAsync(
                    item => item.MediaType == mediaType && item.TmdbId == details.Id,
                    cancellationToken);

            var isNew = media is null;

            if (media is null)
            {
                media = MediaMappings.ToMediaItem(mediaType, details, now);
                _context.MediaItems.Add(media);
            }
            else
            {
                MediaMappings.ApplyDetails(media, details, now);
            }

            await SyncGenresAsync(media, details.Genres, cancellationToken);

            if (mediaType == MediaType.Tv)
            {
                await SyncSeasonSummariesAsync(media, details.Seasons, cancellationToken);
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return media;
            }
            catch (DbUpdateException) when (isNew)
            {
                // Another request inserted the same title first. Discard our
                // insert and return the winning row.
                _context.ChangeTracker.Clear();

                var winner = await _context.MediaItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        item => item.MediaType == mediaType && item.TmdbId == details.Id,
                        cancellationToken);

                if (winner is not null)
                {
                    return winner;
                }

                throw;
            }
        }

        public async Task<MediaItem> RefreshAsync(
            MediaType mediaType,
            int tmdbId,
            string language,
            CancellationToken cancellationToken = default)
        {
            var details = await _tmdbService.GetDetailsAsync(
                mediaType,
                tmdbId,
                language,
                cancellationToken);

            return await UpsertAsync(mediaType, details, cancellationToken);
        }

        public async Task EnsureSeasonEpisodesAsync(
            int mediaItemId,
            int seasonNumber,
            string language,
            CancellationToken cancellationToken = default)
        {
            var media = await _context.MediaItems
                .FirstOrDefaultAsync(item => item.Id == mediaItemId, cancellationToken)
                ?? throw new InvalidOperationException("Media item is not cached.");

            if (media.MediaType != MediaType.Tv)
            {
                throw new InvalidOperationException("Only TV shows have seasons.");
            }

            var season = await _context.TvSeasons
                .FirstOrDefaultAsync(
                    item => item.MediaItemId == mediaItemId
                        && item.SeasonNumber == seasonNumber,
                    cancellationToken);

            if (season is not null &&
                await _context.TvEpisodes.AnyAsync(
                    episode => episode.TvSeasonId == season.Id,
                    cancellationToken))
            {
                return;
            }

            var details = await _tmdbService.GetSeasonAsync(
                media.TmdbId,
                seasonNumber,
                language,
                cancellationToken);

            if (season is null)
            {
                season = new TvSeason
                {
                    MediaItemId = media.Id,
                    SeasonNumber = details.SeasonNumber,
                    Name = details.Name,
                    Overview = details.Overview,
                    PosterPath = details.PosterPath,
                    AirDate = MediaMappings.ParseDate(details.AirDate),
                    EpisodeCount = details.Episodes.Count
                };

                _context.TvSeasons.Add(season);
            }
            else
            {
                season.Name = details.Name;
                season.Overview = details.Overview;
                season.PosterPath = details.PosterPath;
                season.AirDate = MediaMappings.ParseDate(details.AirDate);
                season.EpisodeCount = details.Episodes.Count > 0
                    ? details.Episodes.Count
                    : season.EpisodeCount;
            }

            var existingEpisodes = await _context.TvEpisodes
                .Where(episode => episode.MediaItemId == mediaItemId
                    && episode.SeasonNumber == seasonNumber)
                .ToDictionaryAsync(
                    episode => episode.EpisodeNumber,
                    cancellationToken);

            foreach (var source in details.Episodes)
            {
                if (!existingEpisodes.TryGetValue(source.EpisodeNumber, out var episode))
                {
                    episode = new TvEpisode
                    {
                        MediaItem = media,
                        TvSeason = season,
                        SeasonNumber = source.SeasonNumber == 0
                            ? seasonNumber
                            : source.SeasonNumber,
                        EpisodeNumber = source.EpisodeNumber
                    };

                    _context.TvEpisodes.Add(episode);
                    existingEpisodes[source.EpisodeNumber] = episode;
                }

                episode.TmdbId = source.Id;
                episode.Name = source.Name;
                episode.Overview = source.Overview;
                episode.StillPath = source.StillPath;
                episode.AirDate = MediaMappings.ParseDate(source.AirDate);
                episode.RuntimeMinutes = source.Runtime;
                episode.VoteAverage = source.VoteAverage;
                episode.VoteCount = source.VoteCount;
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A concurrent request cached the season first. If the season
                // now has episodes the request can proceed safely.
                _context.ChangeTracker.Clear();

                var cached = await _context.TvEpisodes.AnyAsync(
                    episode => episode.MediaItemId == mediaItemId
                        && episode.SeasonNumber == seasonNumber,
                    cancellationToken);

                if (!cached)
                {
                    throw;
                }
            }
        }

        public async Task<TvEpisode?> GetOrCreateEpisodeAsync(
            int mediaItemId,
            int seasonNumber,
            int episodeNumber,
            string language,
            CancellationToken cancellationToken = default)
        {
            await EnsureSeasonEpisodesAsync(
                mediaItemId,
                seasonNumber,
                language,
                cancellationToken);

            return await _context.TvEpisodes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    episode => episode.MediaItemId == mediaItemId
                        && episode.SeasonNumber == seasonNumber
                        && episode.EpisodeNumber == episodeNumber,
                    cancellationToken);
        }

        private async Task SyncGenresAsync(
            MediaItem media,
            IEnumerable<TmdbGenre> genres,
            CancellationToken cancellationToken)
        {
            var targetGenres = genres
                .GroupBy(genre => genre.Id)
                .ToDictionary(group => group.Key, group => group.First());

            var genreIds = targetGenres.Keys.ToList();

            var existingGenres = await _context.Genres
                .Where(genre => genre.MediaType == media.MediaType
                    && genreIds.Contains(genre.TmdbId))
                .ToListAsync(cancellationToken);

            foreach (var link in media.Genres.ToList())
            {
                if (!targetGenres.ContainsKey(link.Genre.TmdbId))
                {
                    media.Genres.Remove(link);
                }
            }

            var linkedGenreIds = media.Genres
                .Select(link => link.Genre.TmdbId)
                .ToHashSet();

            foreach (var (tmdbId, source) in targetGenres)
            {
                if (linkedGenreIds.Contains(tmdbId))
                {
                    continue;
                }

                var genre = existingGenres.FirstOrDefault(item => item.TmdbId == tmdbId);

                if (genre is null)
                {
                    genre = new Genre
                    {
                        TmdbId = source.Id,
                        MediaType = media.MediaType,
                        Name = source.Name
                    };

                    _context.Genres.Add(genre);
                    existingGenres.Add(genre);
                }

                media.Genres.Add(new MediaItemGenre
                {
                    MediaItem = media,
                    Genre = genre
                });
            }
        }

        private async Task SyncSeasonSummariesAsync(
            MediaItem media,
            IEnumerable<TmdbSeasonSummary> seasons,
            CancellationToken cancellationToken)
        {
            var targets = seasons
                .GroupBy(season => season.SeasonNumber)
                .ToDictionary(group => group.Key, group => group.First());

            var existing = await _context.TvSeasons
                .Where(season => season.MediaItemId == media.Id)
                .ToListAsync(cancellationToken);

            foreach (var (seasonNumber, source) in targets)
            {
                var season = existing.FirstOrDefault(item =>
                    item.SeasonNumber == seasonNumber);

                if (season is null)
                {
                    season = new TvSeason
                    {
                        // Use the navigation rather than the numeric FK: a
                        // brand-new show has Id == 0 until SaveChanges runs.
                        MediaItem = media,
                        SeasonNumber = seasonNumber
                    };

                    _context.TvSeasons.Add(season);
                    existing.Add(season);
                }

                season.Name = source.Name;
                season.Overview = source.Overview;
                season.PosterPath = source.PosterPath;
                season.AirDate = MediaMappings.ParseDate(source.AirDate);
                season.EpisodeCount = source.EpisodeCount;
            }
        }
    }
}
