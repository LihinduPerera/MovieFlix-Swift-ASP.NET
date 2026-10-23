namespace MovieFlix.Api.DTOs.Preferences
{
    public class PreferencesResponse
    {
        public string PreferredLanguage { get; set; } = "en-US";

        public string PreferredRegion { get; set; } = "US";

        public string Theme { get; set; } = "system";

        public bool AutoPlayTrailers { get; set; }

        public bool EmailNotifications { get; set; }

        public bool MatureContentFilter { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
