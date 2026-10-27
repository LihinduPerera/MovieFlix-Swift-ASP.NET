namespace MovieFlix.Api.Exceptions
{
    /// <summary>The account is temporarily locked out after failed sign-ins.</summary>
    public class LockedOutException : Exception
    {
        public LockedOutException()
            : base("This account is temporarily locked. Try again later.")
        {
        }
    }
}
