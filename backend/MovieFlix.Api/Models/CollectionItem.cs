namespace MovieFlix.Api.Models;

/// <summary>Join entity linking a <see cref="Collection"/> to a <see cref="MediaItem"/>.</summary>
public class CollectionItem
{
    public int Id { get; set; }

    public int CollectionId { get; set; }

    public int MediaItemId { get; set; }

    public DateTime AddedAt { get; set; }

    public Collection Collection { get; set; } = null!;

    public MediaItem MediaItem { get; set; } = null!;
}
