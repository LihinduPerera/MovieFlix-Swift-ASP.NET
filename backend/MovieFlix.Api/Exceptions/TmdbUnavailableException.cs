namespace MovieFlix.Api.Exceptions
{
    /// <summary>
    /// The TMDB circuit is open, a timeout fired, or the network was
    /// unreachable. Surfaced as HTTP 503.
    /// </summary>
    public class TmdbUnavailableException : Exception
    {
        public TmdbUnavailableException()
            : base("The movie provider is currently unavailable.")
        {
        }

        public TmdbUnavailableException(Exception innerException)
            : base("The movie provider is currently unavailable.", innerException)
        {
        }
    }
}
