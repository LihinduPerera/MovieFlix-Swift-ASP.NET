namespace MovieFlix.Api.Models;

/// <summary>Join entity linking a <see cref="MediaItem"/> to a <see cref="Genre"/>.</summary>
public class MediaItemGenre
{
    public int MediaItemId { get; set; }

    public int GenreId { get; set; }

    public MediaItem MediaItem { get; set; } = null!;

    public Genre Genre { get; set; } = null!;
}
