namespace MovieFlix.Api.Exceptions
{
    /// <summary>TMDB returned 404 for the requested resource.</summary>
    public class TmdbNotFoundException : Exception
    {
        public TmdbNotFoundException()
            : base("The requested title was not found in TMDB.")
        {
        }
    }
}
