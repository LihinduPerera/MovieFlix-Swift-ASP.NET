namespace MovieFlix.Api.Exceptions
{
    /// <summary>The caller is authenticated but does not own the resource.</summary>
    public class ForbiddenAccessException : Exception
    {
        public ForbiddenAccessException()
            : base("You do not have access to this resource.")
        {
        }
    }
}
