namespace MovieFlix.Api.Models;

public class Collection
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public ICollection<CollectionMovie> Movies { get; set; } = [];
}
