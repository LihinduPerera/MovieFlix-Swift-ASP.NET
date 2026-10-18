namespace MovieFlix.Api.Exceptions
{
    public class ReviewAlreadyExistsException : Exception
    {
        public ReviewAlreadyExistsException(string userId, int movieId)
            : base($"User {userId} has already reviewed movie {movieId}.")
        {

        }
    }
}
