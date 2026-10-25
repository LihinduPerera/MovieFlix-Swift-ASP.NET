namespace MovieFlix.Api.Models;

/// <summary>A TMDB genre for either movies or TV shows.</summary>
public class Genre
{
    public int Id { get; set; }

    public int TmdbId { get; set; }

    public MediaType MediaType { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<MediaItemGenre> MediaItems { get; set; } = [];
}
