namespace MovieFlix.Api.Models;

/// <summary>A user-created collection of movies and TV shows.</summary>
public class Collection
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Upper-cased, trimmed name used for case-insensitive uniqueness.</summary>
    public string NormalizedName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public ICollection<CollectionItem> Items { get; set; } = [];
}
