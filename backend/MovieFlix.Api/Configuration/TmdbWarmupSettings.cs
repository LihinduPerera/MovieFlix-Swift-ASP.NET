using MovieFlix.Api.Models;

namespace MovieFlix.Api.Configuration
{
    /// <summary>A title to warm in the local media cache at startup.</summary>
    public class TmdbWarmupItem
    {
        public MediaType MediaType { get; set; }

        public int TmdbId { get; set; }
    }

    /// <summary>
    /// Optional background warm-up of the local media cache. Disabled by
    /// default so local runs and tests never make surprise TMDB calls.
    /// </summary>
    public class TmdbWarmupSettings
    {
        public bool Enabled { get; set; }

        /// <summary>
        /// When true, episode metadata for every non-special season is also
        /// warmed. This makes next-episode resolution accurate but issues one
        /// TMDB call per season, so leave it off for large shows.
        /// </summary>
        public bool WarmSeasons { get; set; }

        public List<TmdbWarmupItem> Items { get; set; } = [];
    }
}
