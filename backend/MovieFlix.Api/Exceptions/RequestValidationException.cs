namespace MovieFlix.Api.Exceptions
{
    /// <summary>
    /// A service-level validation failure. Carries a field -> messages map so
    /// the global handler can emit the same RFC 7807 shape as DataAnnotations.
    /// </summary>
    public class RequestValidationException : Exception
    {
        public RequestValidationException(string field, string message)
            : base("One or more validation errors occurred.")
        {
            Errors = new Dictionary<string, string[]>
            {
                [field] = [message]
            };
        }

        public RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
            : base("One or more validation errors occurred.")
        {
            Errors = errors;
        }

        public IReadOnlyDictionary<string, string[]> Errors { get; }
    }
}
