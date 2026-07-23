using System.Text;
using System.Text.RegularExpressions;

namespace Crawl4AiMcp.IO;

/// <summary>
/// Writes crawl4ai artifacts to an agent-supplied output directory. Handles
/// directory creation, filename derivation/sanitization (blocking path
/// traversal), collision-safe naming and BOM-free UTF-8 text output.
/// </summary>
public sealed partial class ArtifactWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public readonly record struct WriteResult(string Path, long Bytes);

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
    /// creating the directory if needed. A supplied <paramref name="fileName"/> is reduced to its
    /// leaf name (defeating "..\" traversal); otherwise a slug is derived from the URL.
    /// </summary>
    public string ResolveOutputPath(string outputDirectory, string? fileName, string url, string extension)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("outputDirectory is required and must not be empty.", nameof(outputDirectory));

        var directory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(directory);

        string baseName;
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            // Strip any directory components the agent may have included.
            baseName = Path.GetFileNameWithoutExtension(Path.GetFileName(fileName.Trim()));
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = Slug(url);
        }
        else
        {
            baseName = Slug(url);
        }

        baseName = Sanitize(baseName);

        var candidate = Path.Combine(directory, baseName + extension);
        var counter = 1;
        while (File.Exists(candidate))
            candidate = Path.Combine(directory, $"{baseName}-{counter++}{extension}");

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
