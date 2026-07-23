using Crawl4AiMcp.Configuration;
using Xunit;

namespace Crawl4AiMcp.Tests;

public class PathPolicyTests
{
    private static PathPolicy Policy(params string[] patterns) =>
        new(new Crawl4AiOptions { AllowedOutputPatterns = new List<string>(patterns) });

    [Fact]
    public void EmptyPatterns_DenyAll()
    {
        var policy = Policy();
        Assert.False(policy.HasOutputPatterns);
        Assert.False(policy.IsOutputAllowed("C:\\anything\\at\\all"));
    }

    [Fact]
    public void Matching_Pattern_IsAllowed()
    {
        var policy = Policy(@"^C:\\crawl-out(\\|$)");
        Assert.True(policy.IsOutputAllowed("C:\\crawl-out"));
        Assert.True(policy.IsOutputAllowed("C:\\crawl-out\\sub\\dir"));
    }

    [Fact]
    public void NonMatching_Pattern_IsDenied()
    {
        var policy = Policy(@"^C:\\crawl-out(\\|$)");
        Assert.False(policy.IsOutputAllowed("C:\\somewhere\\else"));
    }

    [Fact]
    public void Matching_IsCaseInsensitive()
    {
        var policy = Policy(@"^C:\\Crawl-Out(\\|$)");
        Assert.True(policy.IsOutputAllowed("c:\\crawl-out\\x"));
    }
}
