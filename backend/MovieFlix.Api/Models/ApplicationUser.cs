using Microsoft.AspNetCore.Identity;

namespace MovieFlix.Api.Models;

/// <summary>
/// The application's user entity. Extends ASP.NET Core Identity's
/// <see cref="IdentityUser"/> (string/GUID primary key) with the extra
/// profile fields MovieFlix needs.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
