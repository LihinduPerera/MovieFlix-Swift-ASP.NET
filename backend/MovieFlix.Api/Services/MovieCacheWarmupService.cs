using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;

namespace MovieFlix.Api.Services
{
    /// <summary>
    /// Optional hosted service that pre-populates the local movie cache at
    /// startup so the first user request for a popular title is served from
    /// the database instead of TMDB.
    ///
    /// It only runs when <c>Tmdb:Warmup:Enabled</c> is <c>true</c> and at least
    /// one id is configured. Failures are logged and never crash the app.
    /// </summary>
    public class MovieCacheWarmupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TmdbWarmupSettings _settings;
        private readonly ILogger<MovieCacheWarmupService> _logger;

        public MovieCacheWarmupService(
            IServiceScopeFactory scopeFactory,
            IOptions<TmdbWarmupSettings> settings,
            ILogger<MovieCacheWarmupService> logger)
        {
            _scopeFactory = scopeFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_settings.Enabled || _settings.MovieIds.Count == 0)
            {
                _logger.LogDebug("TMDB cache warm-up is disabled or has no ids.");
                return;
            }

            _logger.LogInformation(
                "Starting TMDB cache warm-up for {Count} movie(s).",
                _settings.MovieIds.Count);

            // A scoped scope is required because IMovieCacheService depends on
            // the scoped DbContext, while this service is a singleton.
            using var scope = _scopeFactory.CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<IMovieCacheService>();

            foreach (var tmdbId in _settings.MovieIds.Distinct())
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await cache.GetOrCreateByTmdbIdAsync(tmdbId, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "TMDB cache warm-up failed for movie {TmdbId}.",
                        tmdbId);
                }
            }

            _logger.LogInformation("TMDB cache warm-up finished.");
        }
    }
}
