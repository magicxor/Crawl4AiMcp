namespace Crawl4AiMcp.Validation;

/// <summary>
/// Pure (no-IO) validation helpers for output paths. Every failure throws a
/// <see cref="PathValidationException"/> whose message explains exactly what is wrong so
/// the calling agent can correct the request. Adapted from the FileMcp path validator.
/// </summary>
public static class PathValidator
{
    private static readonly char[] Separators =
    {
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar,
    };

    /// <summary>
    /// Validates a bare file name: non-empty, not made up solely of dots ('.', '..', '...'),
    /// free of characters that are invalid in a file name (which also rules out directory
    /// separators), and equal to its own leaf name (no directory component smuggled in).
    /// </summary>
    public static void ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new PathValidationException("fileName must not be empty.");
        }

        if (fileName.Trim('.').Length == 0)
        {
            throw new PathValidationException(
                $"fileName '{fileName}' must be a real file name, not '.'/'..' or a string of only dots.");
        }

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new PathValidationException(
                $"fileName '{fileName}' contains invalid characters or a path separator; " +
                "it must be a single file name (for example 'report').");
        }

        // Defense in depth: reject anything that is not exactly its own leaf name.
        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new PathValidationException(
                $"fileName '{fileName}' must not contain any directory component.");
        }
    }

    /// <summary>
    /// Validates a directory argument: it must be an absolute (fully-qualified, rooted) path,
    /// contain no characters that are invalid in a path, and contain no empty segment, no
    /// '.'/'..' segment, and no all-dots segment. The raw string is validated as-is; it is
    /// intentionally never normalized with <see cref="Path.GetFullPath"/>, because
    /// normalization would silently collapse ".." instead of rejecting it.
    /// </summary>
    /// <param name="path">The directory path to validate.</param>
    /// <param name="argName">Argument name used in error messages (e.g. "outputDirectory").</param>
    public static void ValidateDirectory(string path, string argName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new PathValidationException($"{argName} must not be empty.");
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new PathValidationException(
                $"{argName} must be an absolute, rooted path (for example 'C:\\data\\out'). " +
                $"'{path}' is not fully qualified.");
        }

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new PathValidationException($"{argName} '{path}' contains invalid path characters.");
        }

        // Split off the root (e.g. "C:\" or "\\server\share\") and inspect the remaining
        // segments only; the root legitimately contains ':' which is not a valid file-name char.
        var root = Path.GetPathRoot(path) ?? string.Empty;
        var remainder = path[root.Length..];

        // GetPathRoot omits the separator that joins the root to the first segment for some
        // path forms (notably UNC "\\server\share"); drop that single boundary separator so a
        // legitimate path is not mistaken for having a leading empty segment.
        if (remainder.Length > 0 && Array.IndexOf(Separators, remainder[0]) >= 0)
        {
            remainder = remainder[1..];
        }

        // A single trailing separator is benign, e.g. "C:\data\out\". Trim exactly one (via the
        // purpose-built helper) rather than all of them, so a doubled trailing separator
        // ("...out\\") still surfaces as an empty segment below and is rejected.
        remainder = Path.TrimEndingDirectorySeparator(remainder);
        if (remainder.Length == 0)
        {
            return; // Path is just the root, e.g. "C:\" or "\\server\share\".
        }

        // Do NOT use RemoveEmptyEntries: an empty segment (a double separator) is trash we reject.
        var segments = remainder.Split(Separators);
        var invalidNameChars = Path.GetInvalidFileNameChars();
        foreach (var segment in segments)
        {
            if (segment.Length == 0)
            {
                throw new PathValidationException(
                    $"{argName} '{path}' must not contain empty path segments (e.g. from a double separator).");
            }

            if (segment.Trim('.').Length == 0)
            {
                throw new PathValidationException(
                    $"{argName} '{path}' must not contain '.', '..' or all-dots path segments.");
            }

            if (segment.IndexOfAny(invalidNameChars) >= 0)
            {
                throw new PathValidationException(
                    $"{argName} '{path}' contains an invalid character in segment '{segment}'.");
            }
        }
    }
}
