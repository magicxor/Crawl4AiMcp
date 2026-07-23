using System.Text;
using System.Text.RegularExpressions;
using Crawl4AiMcp.Configuration;
using Crawl4AiMcp.Validation;

namespace Crawl4AiMcp.IO;

/// <summary>
/// Writes crawl4ai artifacts to an agent-supplied output directory. This is the single
/// choke point for every file write: it enforces path validation (absolute/rooted, no
/// '.'/'..'/empty/all-dots segments, no invalid characters) and the configured output
/// allow-list before touching the filesystem, then handles filename derivation,
/// collision-safe naming and BOM-free UTF-8 text output.
/// </summary>
public sealed partial class ArtifactWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly PathPolicy _policy;

    public ArtifactWriter(PathPolicy policy) => _policy = policy;

    public readonly record struct WriteResult(string Path, long Bytes);

    /// <summary>
    /// Validates an output directory (syntax + allow-list) without writing anything.
    /// Tools call this up front so a bad/blocked directory fails before any network call.
    /// Throws <see cref="PathValidationException"/> on rejection.
    /// </summary>
    public void EnsureOutputDirectoryAllowed(string outputDirectory)
    {
        PathValidator.ValidateDirectory(outputDirectory, "outputDirectory");

        if (!_policy.IsOutputAllowed(outputDirectory))
        {
            throw new PathValidationException(_policy.HasOutputPatterns
                ? $"outputDirectory '{outputDirectory}' is not allowed: it does not match any configured " +
                  "Crawl4Ai:AllowedOutputPatterns."
                : "outputDirectory is not allowed: no Crawl4Ai:AllowedOutputPatterns are configured, so every " +
                  "output directory is blocked. Configure Crawl4Ai:AllowedOutputPatterns to permit specific paths.");
        }
    }

    public async Task<WriteResult> WriteTextAsync(
        string outputDirectory, string? fileName, string url, string extension, string content, CancellationToken ct)
    {
        var path = ResolveOutputPath(outputDirectory, fileName, url, extension);
        await File.WriteAllTextAsync(path, content, Utf8NoBom, ct);
        return new WriteResult(path, new FileInfo(path).Length);
    }

    public async Task<WriteResult> WriteBytesAsync(
        string outputDirectory, string? fileName, string url, string extension, byte[] data, CancellationToken ct)
    {
        var path = ResolveOutputPath(outputDirectory, fileName, url, extension);
        await File.WriteAllBytesAsync(path, data, ct);
        return new WriteResult(path, data.LongLength);
    }

    /// <summary>
    /// Resolves an absolute, collision-free file path inside <paramref name="outputDirectory"/>,
    /// creating the directory if needed. Enforces the full path policy (validation + allow-list)
    /// and validates any supplied <paramref name="fileName"/> as a bare leaf name; otherwise a
    /// slug is derived from the URL. Throws <see cref="PathValidationException"/> on rejection.
    /// </summary>
    public string ResolveOutputPath(string outputDirectory, string? fileName, string url, string extension)
    {
        // Guaranteed guard for every write, even if a caller forgot the up-front check.
        EnsureOutputDirectoryAllowed(outputDirectory);

        string baseName;
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            PathValidator.ValidateFileName(fileName);
            baseName = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = Slug(url);
        }
        else
        {
            baseName = Slug(url);
        }

        baseName = Sanitize(baseName);

        Directory.CreateDirectory(outputDirectory);

        var candidate = Path.Combine(outputDirectory, baseName + extension);
        var counter = 1;
        while (File.Exists(candidate))
            candidate = Path.Combine(outputDirectory, $"{baseName}-{counter++}{extension}");

        return candidate;
    }

    /// <summary>Short, whitespace-collapsed leading snippet for at-a-glance previews.</summary>
    public static string Preview(string? text, int max = 500)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var collapsed = WhitespaceRegex().Replace(text, " ").Trim();
        return collapsed.Length <= max ? collapsed : collapsed[..max] + "…";
    }

    /// <summary>Re-serializes JSON with indentation for a human-readable on-disk file.</summary>
    public static string PrettifyJson(string raw)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            return System.Text.Json.JsonSerializer.Serialize(
                doc.RootElement, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return raw;
        }
    }

    private static string Slug(string url)
    {
        var source = Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
            ? uri.Host + uri.AbsolutePath
            : url;

        var builder = new StringBuilder(source.Length);
        foreach (var ch in source.ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '-');

        var slug = CollapseDashRegex().Replace(builder.ToString(), "-").Trim('-');
        if (slug.Length > 60)
            slug = slug[..60].Trim('-');

        return string.IsNullOrWhiteSpace(slug) ? "page" : slug;
    }

    private static string Sanitize(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '-');

        name = name.Trim().Trim('.');
        return string.IsNullOrWhiteSpace(name) ? "artifact" : name;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex CollapseDashRegex();
}
