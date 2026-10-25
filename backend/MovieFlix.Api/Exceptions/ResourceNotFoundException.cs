namespace MovieFlix.Api.Exceptions
{
    /// <summary>A requested local resource (episode, collection, review) does not exist.</summary>
    public class ResourceNotFoundException : Exception
    {
        public ResourceNotFoundException(string message)
            : base(message)
        {
        }
    }
}
