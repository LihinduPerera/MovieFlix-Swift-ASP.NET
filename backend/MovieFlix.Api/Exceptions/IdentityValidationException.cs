using Microsoft.AspNetCore.Identity;

namespace MovieFlix.Api.Exceptions
{
    /// <summary>
    /// Raised when ASP.NET Core Identity rejects an operation (e.g. weak
    /// password on registration). Exposes a field-keyed errors dictionary.
    /// </summary>
    public class IdentityValidationException : Exception
    {
        public IdentityValidationException(IEnumerable<IdentityError> errors)
            : base("One or more identity validation errors occurred.")
        {
            var grouped = new Dictionary<string, List<string>>();

            foreach (var error in errors)
            {
                var field = error.Code switch
                {
                    "DuplicateEmail" or "DuplicateUserName" or "InvalidEmail" or "InvalidUserName" => "Email",
                    "PasswordTooShort" or "PasswordRequiresDigit" or "PasswordRequiresLower"
                        or "PasswordRequiresUpper" or "PasswordRequiresNonAlphanumeric"
                        or "PasswordMismatch" => "Password",
                    _ => "Identity"
                };

                if (!grouped.TryGetValue(field, out var messages))
                {
                    messages = [];
                    grouped[field] = messages;
                }

                messages.Add(error.Description);
            }

            Errors = grouped.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray());
        }

        public IReadOnlyDictionary<string, string[]> Errors { get; }
    }
}
