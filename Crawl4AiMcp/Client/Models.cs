using System.Text.Json;
using System.Text.Json.Serialization;

namespace Crawl4AiMcp.Client;

// ── Request bodies (serialized to snake_case JSON) ───────────────────────────

/// <summary>Body for POST /md. Mirrors crawl4ai's MarkdownRequest.</summary>
public sealed record MarkdownRequest
{
    public required string Url { get; init; }

    /// <summary>Content-filter strategy: fit, raw, bm25 or llm.</summary>
    [JsonPropertyName("f")] public string Filter { get; init; } = "fit";

    /// <summary>Query used by the bm25/llm filters.</summary>
    [JsonPropertyName("q")] public string? Query { get; init; }

    /// <summary>Cache-bust / revision counter.</summary>
    [JsonPropertyName("c")] public string? CacheBust { get; init; } = "0";

    public string? Provider { get; init; }

    public double? Temperature { get; init; }
}

/// <summary>Body for POST /screenshot. Mirrors crawl4ai's ScreenshotRequest.</summary>
public sealed record ScreenshotRequest
{
    public required string Url { get; init; }
    public double? ScreenshotWaitFor { get; init; }
    public bool? WaitForImages { get; init; }
}

/// <summary>Body for POST /crawl. Mirrors crawl4ai's CrawlRequest (hooks omitted).</summary>
public sealed record CrawlRequest
{
    public required IReadOnlyList<string> Urls { get; init; }
    public JsonElement? BrowserConfig { get; init; }
    public JsonElement? CrawlerConfig { get; init; }
    public JsonElement? CrawlerConfigs { get; init; }
}

// ── Response bodies (only the fields we consume are declared; extras ignored) ─

public sealed record MarkdownResponse
{
    public string Markdown { get; init; } = "";
    public bool Success { get; init; }
}

public sealed record HtmlResponse
{
    public string Html { get; init; } = "";
    public bool Success { get; init; }
}

public sealed record ScreenshotResponse
{
    public bool Success { get; init; }

    /// <summary>Base64-encoded PNG.</summary>
    public string Screenshot { get; init; } = "";

    public string? Mime { get; init; }
    public long Size { get; init; }
}

public sealed record PdfResponse
{
    public bool Success { get; init; }

    /// <summary>Base64-encoded PDF.</summary>
    public string Pdf { get; init; } = "";

    public string? Mime { get; init; }
    public long Size { get; init; }
}
