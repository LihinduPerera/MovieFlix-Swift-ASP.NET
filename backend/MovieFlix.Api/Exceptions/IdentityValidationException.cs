using Microsoft.AspNetCore.Identity;

namespace MovieFlix.Api.Exceptions
{
    /// <summary>
    /// Raised when ASP.NET Core Identity rejects an operation (e.g. weak
    /// password on registration). Carries the individual Identity errors so the
    /// global handler can surface them as validation problems.
    /// </summary>
    public class IdentityValidationException : Exception
    {
        public IReadOnlyList<IdentityError> Errors { get; }

        public IdentityValidationException(IEnumerable<IdentityError> errors)
            : base("One or more identity validation errors occurred.")
        {
            Errors = errors.ToList();
        }
    }
}
