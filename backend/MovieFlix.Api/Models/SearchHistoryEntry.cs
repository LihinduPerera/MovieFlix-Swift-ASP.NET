namespace MovieFlix.Api.Models;

public class SearchHistoryEntry
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>The search term as the user typed it (last-used casing/whitespace preserved).</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Upper-cased, trimmed form of <see cref="Query"/> used to de-duplicate
    /// terms case-insensitively (SQLite unique indexes are case-sensitive).
    /// </summary>
    public string NormalizedQuery { get; set; } = string.Empty;

    public DateTime SearchedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
