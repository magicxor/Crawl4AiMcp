namespace Crawl4AiMcp.Client;

/// <summary>
/// Thrown when the crawl4ai REST API returns a non-success HTTP status.
/// </summary>
public sealed class Crawl4AiApiException : Exception
{
    public int StatusCode { get; }

    public Crawl4AiApiException(int statusCode, string detail)
        : base($"crawl4ai returned HTTP {statusCode}: {detail}")
    {
        StatusCode = statusCode;
    }
}
