namespace MovieFlix.Api.Exceptions
{
    /// <summary>
    /// Raised when an operation would create a duplicate (unique-constraint)
    /// resource, such as adding the same movie to a collection twice.
    /// </summary>
    public class DuplicateResourceException : Exception
    {
        public DuplicateResourceException(string message) : base(message)
        {

        }
    }
}
