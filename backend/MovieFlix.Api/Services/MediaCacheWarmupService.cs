using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services
{
    /// <summary>
    /// Optionally warms a small set of titles at startup. Disabled by default;
    /// failures are logged and never block API availability.
    /// </summary>
    public class MediaCacheWarmupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TmdbWarmupSettings _warmup;
        private readonly TmdbSettings _tmdb;
        private readonly ILogger<MediaCacheWarmupService> _logger;

        public MediaCacheWarmupService(
            IServiceScopeFactory scopeFactory,
            IOptions<TmdbWarmupSettings> warmupOptions,
            IOptions<TmdbSettings> tmdbOptions,
            ILogger<MediaCacheWarmupService> logger)
        {
            _scopeFactory = scopeFactory;
            _warmup = warmupOptions.Value;
            _tmdb = tmdbOptions.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_warmup.Enabled || _warmup.Items.Count == 0)
            {
                return;
            }

            await Task.Yield();

            using var scope = _scopeFactory.CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<IMediaCacheService>();
            var context = scope.ServiceProvider.GetRequiredService<Data.MovieFlixDbContext>();

            foreach (var item in _warmup.Items)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    var media = await cache.GetOrCreateAsync(
                        item.MediaType,
                        item.TmdbId,
                        _tmdb.DefaultLanguage,
                        stoppingToken);

                    if (!_warmup.WarmSeasons || item.MediaType != MediaType.Tv)
                    {
                        continue;
                    }

                    var seasons = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                        .ToListAsync(
                            context.TvSeasons
                                .Where(season => season.MediaItemId == media.Id
                                    && season.SeasonNumber > 0),
                            stoppingToken);

                    foreach (var season in seasons)
                    {
                        await cache.EnsureSeasonEpisodesAsync(
                            media.Id,
                            season.SeasonNumber,
                            _tmdb.DefaultLanguage,
                            stoppingToken);
                    }
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        exception,
                        "Cache warmup failed for {MediaType} {TmdbId}.",
                        item.MediaType,
                        item.TmdbId);
                }
            }
        }
    }
}
