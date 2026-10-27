using MovieFlix.Api.DTOs.Media;

namespace MovieFlix.Api.DTOs.Collections
{
    public class CollectionSummaryResponse
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int ItemCount { get; set; }

        /// <summary>Up to four poster paths, newest items first.</summary>
        public List<string> CoverPosterPaths { get; set; } = [];

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    public class CollectionDetailResponse : CollectionSummaryResponse
    {
        public string OwnerDisplayName { get; set; } = string.Empty;

        public bool IsOwner { get; set; }

        public List<CollectionItemResponse> Items { get; set; } = [];
    }

    public class CollectionItemResponse
    {
        public MediaSummaryResponse Media { get; set; } = new();

        public DateTime AddedAt { get; set; }
    }
}
