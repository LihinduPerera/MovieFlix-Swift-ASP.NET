namespace MovieFlix.Api.Configuration
{
    public class TmdbSettings
    {
        public string BaseUrl { get; set; } = string.Empty;

        public string AccessToken { get; set; } = string.Empty;

        /// <summary>Default BCP-47 language used when a request omits one.</summary>
        public string DefaultLanguage { get; set; } = "en-US";

        /// <summary>Default ISO 3166-1 region used when a request omits one.</summary>
        public string DefaultRegion { get; set; } = "US";
    }
}
