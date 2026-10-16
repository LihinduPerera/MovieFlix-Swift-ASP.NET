namespace MovieFlix.Api.Exceptions
{
    public class FavoriteAlreadyExistsException : Exception
    {
        public FavoriteAlreadyExistsException(string userId, int movieId)
            : base($"User {userId} has already favorited movie {movieId}.")
        {

        }
    }
}
