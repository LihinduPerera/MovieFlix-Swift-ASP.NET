namespace MovieFlix.Api.Exceptions
{
    public class ReviewAlreadyExistsException : Exception
    {
        public ReviewAlreadyExistsException()
            : base("You have already reviewed this title.")
        {
        }
    }
}
