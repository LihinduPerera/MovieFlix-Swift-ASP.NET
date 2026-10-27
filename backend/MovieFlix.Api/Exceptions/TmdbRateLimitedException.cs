namespace MovieFlix.Api.Exceptions
{
    /// <summary>TMDB rate limit (HTTP 429). Surfaced as 503 with Retry-After.</summary>
    public class TmdbRateLimitedException : Exception
    {
        public TmdbRateLimitedException(int retryAfterSeconds)
            : base("The movie provider is rate limiting requests. Try again later.")
        {
            RetryAfterSeconds = retryAfterSeconds > 0 ? retryAfterSeconds : 60;
        }

        public int RetryAfterSeconds { get; }
    }
}
