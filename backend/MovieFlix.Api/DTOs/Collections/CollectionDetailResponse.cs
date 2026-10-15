namespace MovieFlix.Api.DTOs.Collections
{
    public class CollectionDetailResponse
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string OwnerId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public List<CollectionMovieResponse> Movies { get; set; } = [];
    }

    public class CollectionMovieResponse
    {
        public int MovieId { get; set; }

        public int TmdbId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Overview { get; set; } = string.Empty;

        public DateTime AddedAt { get; set; }
    }
}
