namespace MovieFlix.Api.Models;

public class Review
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int MovieId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Movie Movie { get; set; } = null!;
}
