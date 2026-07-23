using Crawl4AiMcp.Validation;
using Xunit;

namespace Crawl4AiMcp.Tests;

public class PathValidatorTests
{
    [Theory]
    [InlineData("report")]
    [InlineData("report.md")]
    [InlineData("a.txt")]
    [InlineData("no-extension")]
    public void ValidateFileName_AcceptsPlainNames(string fileName)
    {
        PathValidator.ValidateFileName(fileName); // does not throw
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("...")]              // all dots
    [InlineData("sub/report.md")]    // separator
    [InlineData("sub\\report.md")]   // separator
    [InlineData("bad|name.txt")]     // invalid char
    public void ValidateFileName_RejectsInvalidNames(string fileName)
    {
        Assert.Throws<PathValidationException>(() => PathValidator.ValidateFileName(fileName));
    }

    [Theory]
    [InlineData("C:\\")]                       // drive root is valid
    [InlineData("C:\\data\\out")]
    [InlineData("C:\\data\\out\\")]           // trailing separator is benign
    [InlineData("\\\\server\\share\\folder")] // UNC
    public void ValidateDirectory_AcceptsAbsolutePaths(string path)
    {
        PathValidator.ValidateDirectory(path, "outputDirectory"); // does not throw
    }

    [Theory]
    [InlineData("relative\\path")]     // not rooted
    [InlineData("out")]                 // not rooted
    [InlineData("C:")]                  // drive-relative, not fully qualified ("C:" != "C:\")
    [InlineData("\\data\\out")]        // rooted but not fully qualified (drive-relative)
    [InlineData("C:data\\out")]        // drive-relative, not fully qualified
    [InlineData("C:\\data\\..\\out")]  // ".." segment
    [InlineData("C:\\data\\.\\out")]   // "." segment
    [InlineData("C:\\data\\...\\out")] // all-dots segment
    [InlineData("C:\\data\\\\out")]    // empty segment (double separator)
    [InlineData("C:\\data\\out\\\\")]  // doubled trailing separator (empty segment)
    [InlineData("C:\\data\\ou<t")]     // invalid char in segment
    public void ValidateDirectory_RejectsInvalidPaths(string path)
    {
        Assert.Throws<PathValidationException>(() => PathValidator.ValidateDirectory(path, "outputDirectory"));
    }

    [Fact]
    public void ValidateDirectory_RejectsEmpty()
    {
        Assert.Throws<PathValidationException>(() => PathValidator.ValidateDirectory("", "outputDirectory"));
    }
}
