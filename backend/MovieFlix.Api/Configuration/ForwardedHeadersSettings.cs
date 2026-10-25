namespace MovieFlix.Api.Configuration
{
    /// <summary>
    /// Reverse-proxy trust configuration for X-Forwarded-For / X-Forwarded-Proto.
    /// Only proxies explicitly listed here are trusted.
    /// </summary>
    public class ForwardedHeadersSettings
    {
        public bool Enabled { get; set; }

        /// <summary>Trusted proxy IP addresses (e.g. "10.0.0.10").</summary>
        public List<string> KnownProxies { get; set; } = [];

        /// <summary>Trusted proxy networks in CIDR form (e.g. "10.0.0.0/8").</summary>
        public List<string> KnownNetworks { get; set; } = [];
    }
}
