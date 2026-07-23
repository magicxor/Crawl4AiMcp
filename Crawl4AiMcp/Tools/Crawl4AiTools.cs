using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crawl4AiMcp.Client;
using Crawl4AiMcp.IO;
using ModelContextProtocol.Server;

namespace Crawl4AiMcp.Tools;

/// <summary>
/// MCP tools that proxy to a configured crawl4ai REST instance. Unlike the
/// official crawl4ai MCP (which returns base64 blobs and huge JSON/Markdown
/// inline), these tools write file-like artifacts to the caller-supplied
/// <c>outputDirectory</c> and return only a small summary (paths, metadata and
/// a short preview). The purely-textual <c>ask</c> tool stays inline.
/// </summary>
internal sealed class Crawl4AiTools
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    private readonly Crawl4AiClient _client;
    private readonly ArtifactWriter _writer;

    public Crawl4AiTools(Crawl4AiClient client, ArtifactWriter writer)
    {
        _client = client;
        _writer = writer;
    }

    [McpServerTool(Name = "md")]
    [Description("Convert a web page to Markdown via crawl4ai and save it as a .md file in the given output directory. Returns the file path, byte size and a short preview instead of the full (potentially huge) document.")]
    public async Task<object> Md(
        [Description("Absolute http/https URL to fetch (or raw:/raw:// for raw HTML).")] string url,
        [Description("Directory where the .md file will be written. Created if it does not exist. REQUIRED.")] string outputDirectory,
        [Description("Content-filter strategy: 'fit' (default, clean readable content), 'raw' (direct DOM->Markdown), 'bm25' or 'llm'.")] string filter = "fit",
        [Description("Optional query used by the bm25/llm filters.")] string? query = null,
        [Description("Optional cache-bust / revision counter (default \"0\").")] string? cacheBust = "0",
        [Description("Optional LLM provider override, e.g. 'anthropic/claude-3-opus' (llm filter only).")] string? provider = null,
        [Description("Optional LLM temperature override 0.0-2.0 (llm filter only).")] double? temperature = null,
        [Description("Optional output file name. If omitted, a name is derived from the URL. Any path components are stripped.")] string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.MarkdownAsync(new MarkdownRequest
            {
                Url = url,
                Filter = filter,
                Query = query,
                CacheBust = cacheBust,
                Provider = provider,
                Temperature = temperature,
            }, cancellationToken);

            var written = await _writer.WriteTextAsync(outputDirectory, fileName, url, ".md", response.Markdown, cancellationToken);

            return new
            {
                success = true,
                url,
                filter,
                query,
                filePath = written.Path,
                bytes = written.Bytes,
                preview = ArtifactWriter.Preview(response.Markdown),
            };
        }
        catch (Exception ex)
        {
            return Error(ex);
        }
    }

    [McpServerTool(Name = "html")]
    [Description("Crawl a URL and return crawl4ai's preprocessed/sanitized HTML (useful for building extraction schemas), saved as an .html file in the output directory. Returns the file path, byte size and a short preview.")]
    public async Task<object> Html(
        [Description("Absolute http/https URL to fetch.")] string url,
        [Description("Directory where the .html file will be written. Created if missing. REQUIRED.")] string outputDirectory,
        [Description("Optional output file name; derived from the URL if omitted. Path components are stripped.")] string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.HtmlAsync(url, cancellationToken);
            var written = await _writer.WriteTextAsync(outputDirectory, fileName, url, ".html", response.Html, cancellationToken);

            return new
            {
                success = true,
                url,
                filePath = written.Path,
                bytes = written.Bytes,
                preview = ArtifactWriter.Preview(response.Html),
            };
        }
        catch (Exception ex)
        {
            return Error(ex);
        }
    }

    [McpServerTool(Name = "screenshot")]
    [Description("Capture a full-page PNG screenshot of a URL via crawl4ai and save it as a .png file in the output directory. Returns the file path and image metadata (the base64 image is NOT returned inline).")]
    public async Task<object> Screenshot(
        [Description("Absolute http/https URL to screenshot.")] string url,
        [Description("Directory where the .png file will be written. Created if missing. REQUIRED.")] string outputDirectory,
        [Description("Seconds to wait before capturing (default 2).")] double? screenshotWaitFor = 2,
        [Description("Wait for all images to finish loading before capture (default false).")] bool? waitForImages = false,
        [Description("Optional output file name; derived from the URL if omitted. Path components are stripped.")] string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.ScreenshotAsync(url, screenshotWaitFor, waitForImages, cancellationToken);
            var bytes = Convert.FromBase64String(response.Screenshot);
            var written = await _writer.WriteBytesAsync(outputDirectory, fileName, url, ".png", bytes, cancellationToken);

            return new
            {
                success = true,
                url,
                filePath = written.Path,
                mime = response.Mime ?? "image/png",
                size = written.Bytes,
            };
        }
        catch (Exception ex)
        {
            return Error(ex);
        }
    }

    [McpServerTool(Name = "pdf")]
    [Description("Render a URL to PDF via crawl4ai and save it as a .pdf file in the output directory. Returns the file path and metadata (the base64 PDF is NOT returned inline).")]
    public async Task<object> Pdf(
        [Description("Absolute http/https URL to render as PDF.")] string url,
        [Description("Directory where the .pdf file will be written. Created if missing. REQUIRED.")] string outputDirectory,
        [Description("Optional output file name; derived from the URL if omitted. Path components are stripped.")] string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.PdfAsync(url, cancellationToken);
            var bytes = Convert.FromBase64String(response.Pdf);
            var written = await _writer.WriteBytesAsync(outputDirectory, fileName, url, ".pdf", bytes, cancellationToken);

            return new
            {
                success = true,
                url,
                filePath = written.Path,
                mime = response.Mime ?? "application/pdf",
                size = written.Bytes,
            };
        }
        catch (Exception ex)
        {
            return Error(ex);
        }
    }

    [McpServerTool(Name = "execute_js")]
    [Description("Execute JavaScript snippets on a page via crawl4ai and save the full crawl result as a .json file in the output directory. Returns the file path, byte size, a short preview and the (usually small) js_execution_result inline. NOTE: the crawl4ai server must be started with CRAWL4AI_EXECUTE_JS_ENABLED=true or this returns an error.")]
    public async Task<object> ExecuteJs(
        [Description("Absolute http/https URL to load.")] string url,
        [Description("Ordered list of JavaScript snippets to run; each should be an expression or IIFE that returns a value.")] string[] scripts,
        [Description("Directory where the .json result file will be written. Created if missing. REQUIRED.")] string outputDirectory,
        [Description("Optional output file name; derived from the URL if omitted. Path components are stripped.")] string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var raw = await _client.ExecuteJsAsync(url, scripts, cancellationToken);
            var pretty = ArtifactWriter.PrettifyJson(raw);
            var written = await _writer.WriteTextAsync(outputDirectory, fileName, url, ".json", pretty, cancellationToken);

            JsonElement? jsResult = null;
            using (var doc = JsonDocument.Parse(raw))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("js_execution_result", out var jre))
                {
                    jsResult = jre.Clone();
                }
            }

            return new
            {
                success = true,
                url,
                filePath = written.Path,
                bytes = written.Bytes,
                jsExecutionResult = jsResult,
                preview = ArtifactWriter.Preview(pretty),
            };
        }
        catch (Exception ex)
        {
            return Error(ex);
        }
    }

    [McpServerTool(Name = "crawl")]
    [Description("Crawl one or more URLs via crawl4ai. For each result the markdown, any screenshot/PDF, and the full per-URL result JSON are written to the output directory (base64 blobs are extracted to separate files). Returns a manifest of the files written - no huge JSON or base64 is returned inline.")]
    public async Task<object> Crawl(
        [Description("List of absolute http/https URLs to crawl (1-100).")] string[] urls,
        [Description("Directory where result files will be written. Created if missing. REQUIRED.")] string outputDirectory,
        [Description("Optional crawl4ai BrowserConfig as a JSON object string.")] string? browserConfig = null,
        [Description("Optional crawl4ai CrawlerRunConfig as a JSON object string.")] string? crawlerConfig = null,
        [Description("Optional list of per-URL CrawlerRunConfig objects as a JSON array string (takes precedence over crawlerConfig).")] string? crawlerConfigs = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CrawlRequest
            {
                Urls = urls,
                BrowserConfig = ParseJson(browserConfig, nameof(browserConfig)),
                CrawlerConfig = ParseJson(crawlerConfig, nameof(crawlerConfig)),
                CrawlerConfigs = ParseJson(crawlerConfigs, nameof(crawlerConfigs)),
            };

            var raw = await _client.CrawlAsync(request, cancellationToken);

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var success = root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
            double? processingTime = root.TryGetProperty("server_processing_time_s", out var pt) && pt.ValueKind == JsonValueKind.Number
                ? pt.GetDouble()
                : null;

            var files = new List<object>();
            if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var result in results.EnumerateArray())
                {
                    index++;
                    var resultUrl = result.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String
                        ? u.GetString() ?? $"result-{index}"
                        : $"result-{index}";

                    var entry = new Dictionary<string, object?>
                    {
                        ["url"] = resultUrl,
                        ["success"] = result.TryGetProperty("success", out var rs) && rs.ValueKind == JsonValueKind.True,
                    };

                    var markdown = ExtractMarkdown(result);
                    if (!string.IsNullOrEmpty(markdown))
                    {
                        var w = await _writer.WriteTextAsync(outputDirectory, null, resultUrl, ".md", markdown, cancellationToken);
                        entry["markdownFile"] = w.Path;
                    }

                    string? screenshotFile = await WriteBase64FieldAsync(result, "screenshot", outputDirectory, resultUrl, ".png", cancellationToken);
                    if (screenshotFile is not null)
                        entry["screenshotFile"] = screenshotFile;

                    string? pdfFile = await WriteBase64FieldAsync(result, "pdf", outputDirectory, resultUrl, ".pdf", cancellationToken);
                    if (pdfFile is not null)
                        entry["pdfFile"] = pdfFile;

                    // Full per-result JSON, with the large base64 blobs replaced by file references.
                    var node = JsonNode.Parse(result.GetRawText())!.AsObject();
                    node.Remove("screenshot");
                    node.Remove("pdf");
                    if (screenshotFile is not null) node["screenshot_file"] = screenshotFile;
                    if (pdfFile is not null) node["pdf_file"] = pdfFile;

                    var jsonWritten = await _writer.WriteTextAsync(
                        outputDirectory, null, resultUrl, ".json", node.ToJsonString(IndentedJson), cancellationToken);
                    entry["jsonFile"] = jsonWritten.Path;

                    files.Add(entry);
                }
            }

            return new
            {
                success,
                count = files.Count,
                serverProcessingTimeS = processingTime,
                files,
            };
        }
        catch (Exception ex)
        {
            return Error(ex);
        }
    }

    [McpServerTool(Name = "ask")]
    [Description("Query crawl4ai's own library context (its code + documentation) to help build crawl4ai configs and usage. A non-empty query is REQUIRED so results stay small; BM25-filtered results are returned inline. This tool does NOT write any files.")]
    public async Task<object> Ask(
        [Description("BM25 search query against crawl4ai's code/docs context. REQUIRED and must be non-empty.")] string query,
        [Description("Which context to search: 'all' (default), 'code' or 'doc'.")] string contextType = "all",
        [Description("Minimum BM25 score ratio, 0.0-1.0 (default 0.5).")] double scoreRatio = 0.5,
        [Description("Maximum number of results to return (default 20).")] int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new
            {
                success = false,
                error = "The 'query' argument is required and must be non-empty. Provide a search query so the result stays small.",
            };
        }

        try
        {
            var raw = await _client.AskAsync(contextType, query, scoreRatio, maxResults, cancellationToken);
            using var doc = JsonDocument.Parse(raw);
            return new
            {
                success = true,
                results = doc.RootElement.Clone(),
            };
        }
        catch (Exception ex)
        {
            return Error(ex);
        }
    }

    private async Task<string?> WriteBase64FieldAsync(
        JsonElement result, string field, string outputDirectory, string url, string extension, CancellationToken ct)
    {
        if (!result.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String)
            return null;

        var base64 = value.GetString();
        if (string.IsNullOrEmpty(base64))
            return null;

        var written = await _writer.WriteBytesAsync(outputDirectory, null, url, extension, Convert.FromBase64String(base64), ct);
        return written.Path;
    }

    private static string? ExtractMarkdown(JsonElement result)
    {
        if (!result.TryGetProperty("markdown", out var markdown))
            return null;

        if (markdown.ValueKind == JsonValueKind.String)
            return markdown.GetString();

        if (markdown.ValueKind == JsonValueKind.Object)
        {
            if (markdown.TryGetProperty("raw_markdown", out var raw) && raw.ValueKind == JsonValueKind.String)
                return raw.GetString();
            if (markdown.TryGetProperty("fit_markdown", out var fit) && fit.ValueKind == JsonValueKind.String)
                return fit.GetString();
        }

        return null;
    }

    private static JsonElement? ParseJson(string? json, string argName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"'{argName}' must be a valid JSON string: {ex.Message}", argName);
        }
    }

    private static object Error(Exception ex) => ex switch
    {
        Crawl4AiApiException api => new { success = false, error = api.Message, statusCode = api.StatusCode },
        _ => new { success = false, error = ex.Message },
    };
}
