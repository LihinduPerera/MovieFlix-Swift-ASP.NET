namespace MovieFlix.Api.Models;

/// <summary>Join entity linking a <see cref="Collection"/> to a <see cref="Movie"/>.</summary>
public class CollectionMovie
{
    public int Id { get; set; }

    public int CollectionId { get; set; }

    public int MovieId { get; set; }

    public DateTime AddedAt { get; set; }

    public Collection Collection { get; set; } = null!;

    public Movie Movie { get; set; } = null!;
}
