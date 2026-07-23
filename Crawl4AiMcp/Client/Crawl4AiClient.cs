using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Crawl4AiMcp.Client;

/// <summary>
/// Typed HttpClient wrapper over the crawl4ai deploy/docker REST API.
/// The base address, timeout and bearer token are configured on the injected
/// HttpClient (see Program.cs). Request/response JSON uses snake_case.
/// </summary>
public sealed class Crawl4AiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public Crawl4AiClient(HttpClient http) => _http = http;

    public Task<MarkdownResponse> MarkdownAsync(MarkdownRequest request, CancellationToken ct)
        => PostAsync<MarkdownResponse>("md", request, ct);

    public Task<HtmlResponse> HtmlAsync(string url, CancellationToken ct)
        => PostAsync<HtmlResponse>("html", new { url }, ct);

    public Task<ScreenshotResponse> ScreenshotAsync(string url, double? waitFor, bool? waitForImages, CancellationToken ct)
        => PostAsync<ScreenshotResponse>("screenshot", new ScreenshotRequest
        {
            Url = url,
            ScreenshotWaitFor = waitFor,
            WaitForImages = waitForImages,
        }, ct);

    public Task<PdfResponse> PdfAsync(string url, CancellationToken ct)
        => PostAsync<PdfResponse>("pdf", new { url }, ct);

    /// <summary>Returns the raw JSON body (a full CrawlResult dump).</summary>
    public Task<string> ExecuteJsAsync(string url, IReadOnlyList<string> scripts, CancellationToken ct)
        => PostRawAsync("execute_js", new { url, scripts }, ct);

    /// <summary>Returns the raw JSON body (contains a "results" array of CrawlResult dicts).</summary>
    public Task<string> CrawlAsync(CrawlRequest request, CancellationToken ct)
        => PostRawAsync("crawl", request, ct);

    /// <summary>Returns the raw JSON body for GET /ask.</summary>
    public Task<string> AskAsync(string contextType, string query, double scoreRatio, int maxResults, CancellationToken ct)
    {
        var relative =
            $"ask?context_type={Uri.EscapeDataString(contextType)}" +
            $"&query={Uri.EscapeDataString(query)}" +
            $"&score_ratio={scoreRatio.ToString(CultureInfo.InvariantCulture)}" +
            $"&max_results={maxResults.ToString(CultureInfo.InvariantCulture)}";
        return GetRawAsync(relative, ct);
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync(path, body, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return result ?? throw new Crawl4AiApiException((int)response.StatusCode, "Empty response body.");
    }

    private async Task<string> PostRawAsync(string path, object body, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync(path, body, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task<string> GetRawAsync(string path, CancellationToken ct)
    {
        using var response = await _http.GetAsync(path, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var detail = "";
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            detail = ExtractDetail(body);
        }
        catch
        {
            // Ignore body-read failures; the status code is still meaningful.
        }

        throw new Crawl4AiApiException((int)response.StatusCode, detail);
    }

    /// <summary>Pulls the "detail"/"error" message out of a crawl4ai error body, else a trimmed snippet.</summary>
    private static string ExtractDetail(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "(no response body)";

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("detail", out var detail))
                    return detail.ToString();
                if (doc.RootElement.TryGetProperty("error", out var error))
                    return error.ToString();
            }
        }
        catch
        {
            // Not JSON; fall through to raw snippet.
        }

        return body.Length > 500 ? body[..500] + "…" : body;
    }
}
