namespace MovieFlix.Api.Configuration
{
    /// <summary>
    /// Tunable rate-limiting limits. Configurable so environments (and tests)
    /// can relax or tighten them without code changes.
    /// </summary>
    public class RateLimitingSettings
    {
        /// <summary>Requests allowed per client per global window.</summary>
        public int GlobalPermitLimit { get; set; } = 120;

        public int GlobalWindowSeconds { get; set; } = 60;

        /// <summary>Requests allowed per client per window on credential endpoints.</summary>
        public int AuthPermitLimit { get; set; } = 10;

        public int AuthWindowSeconds { get; set; } = 60;
    }
}
