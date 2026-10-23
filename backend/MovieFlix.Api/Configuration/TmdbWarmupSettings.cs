namespace MovieFlix.Api.Configuration
{
    /// <summary>
    /// Optional background warm-up of the local movie cache. Disabled by
    /// default so local runs and tests never make surprise TMDB calls.
    /// </summary>
    public class TmdbWarmupSettings
    {
        public bool Enabled { get; set; }

        /// <summary>TMDB movie ids to pre-cache at startup.</summary>
        public List<int> MovieIds { get; set; } = [];
    }
}
