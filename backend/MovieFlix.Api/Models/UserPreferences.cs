namespace MovieFlix.Api.Models;

/// <summary>
/// Per-user application preferences, stored one-to-one with
/// <see cref="ApplicationUser"/>.
/// </summary>
public class UserPreferences
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>BCP-47 language tag used for TMDB metadata, e.g. "en-US".</summary>
    public string PreferredLanguage { get; set; } = "en-US";

    /// <summary>ISO 3166-1 region used for TMDB locale / release dates, e.g. "US".</summary>
    public string PreferredRegion { get; set; } = "US";

    /// <summary>UI theme: "system", "light" or "dark".</summary>
    public string Theme { get; set; } = "system";

    public bool AutoPlayTrailers { get; set; } = true;

    public bool EmailNotifications { get; set; } = true;

    /// <summary>When true, adult / mature titles are hidden from results.</summary>
    public bool MatureContentFilter { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
