namespace Crawl4AiMcp.Configuration;

/// <summary>
/// Configuration for the remote crawl4ai instance this MCP server proxies to.
/// Bound from the "Crawl4Ai" configuration section (appsettings.json or the
/// Crawl4Ai__* environment variables).
/// </summary>
public sealed class Crawl4AiOptions
{
    public const string SectionName = "Crawl4Ai";

    /// <summary>Base URL of the crawl4ai REST instance, e.g. http://localhost:11235.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11235";

    /// <summary>
    /// Optional bearer token (CRAWL4AI_API_TOKEN). When set it is sent as
    /// "Authorization: Bearer &lt;token&gt;" on every request. Leave empty for
    /// unauthenticated loopback instances.
    /// </summary>
    public string? ApiToken { get; set; }

    /// <summary>HTTP timeout in seconds for crawl4ai requests. Crawls can be slow.</summary>
    public int TimeoutSeconds { get; set; } = 300;
}
