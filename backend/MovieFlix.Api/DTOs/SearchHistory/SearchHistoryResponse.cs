namespace MovieFlix.Api.DTOs.SearchHistory
{
    public class SearchHistoryResponse
    {
        public int Id { get; set; }

        public string Query { get; set; } = string.Empty;

        public DateTime SearchedAt { get; set; }
    }
}
