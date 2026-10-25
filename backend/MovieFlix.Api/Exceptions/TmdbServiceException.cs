namespace MovieFlix.Api.Exceptions
{
    /// <summary>
    /// An upstream TMDB call failed for a reason other than 404, rate limiting,
    /// a timeout, or an open circuit. Surfaced as HTTP 502.
    /// </summary>
    public class TmdbServiceException : Exception
    {
        public TmdbServiceException()
            : base("The movie provider could not complete the request.")
        {
        }

        public TmdbServiceException(Exception innerException)
            : base("The movie provider could not complete the request.", innerException)
        {
        }
    }
}
