namespace MovieFlix.Api.Exceptions
{
    /// <summary>
    /// Raised when an authenticated user attempts to act on a resource they do
    /// not own (e.g. editing another user's review).
    /// </summary>
    public class ForbiddenAccessException : Exception
    {
        public ForbiddenAccessException(string message = "You are not allowed to perform this action.")
            : base(message)
        {

        }
    }
}
