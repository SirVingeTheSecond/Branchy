using Branchy.UI.Services;
using Xunit;

namespace Branchy.Test;

public sealed class GitBranchParserTest
{
    [Fact]
    public void Parse_EmptyOutput_ReturnsEmptyList()
    {
        var result = GitBranchParser.Parse("");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_SingleLocalBranch_ReturnsBranch()
    {
        var output = "main|*|refs/heads\n";

        var result = GitBranchParser.Parse(output);

        Assert.Single(result);
        Assert.Equal("main", result[0].Name);
        Assert.True(result[0].IsCurrent);
        Assert.False(result[0].IsRemote);
    }

    [Fact]
    public void Parse_NonCurrentBranch_SetsIsCurrentFalse()
    {
        var output = "feature| |refs/heads\n";

        var result = GitBranchParser.Parse(output);

        Assert.Single(result);
        Assert.Equal("feature", result[0].Name);
        Assert.False(result[0].IsCurrent);
    }

    [Fact]
    public void Parse_RemoteBranch_SetsIsRemoteTrue()
    {
        var output = "origin/main| |refs/remotes\n";

        var result = GitBranchParser.Parse(output);

        Assert.Single(result);
        Assert.Equal("origin/main", result[0].Name);
        Assert.True(result[0].IsRemote);
    }

    [Fact]
    public void Parse_OriginHead_IsSkipped()
    {
        var output = "origin/HEAD| |refs/remotes\n";

        var result = GitBranchParser.Parse(output);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MultipleBranches_ReturnsAll()
    {
        var output = """
            main|*|refs/heads
            feature| |refs/heads
            origin/main| |refs/remotes
            origin/feature| |refs/remotes
            """;

        var result = GitBranchParser.Parse(output);

        Assert.Equal(4, result.Count);
        Assert.Equal("main", result[0].Name);
        Assert.True(result[0].IsCurrent);
        Assert.False(result[0].IsRemote);
        Assert.Equal("feature", result[1].Name);
        Assert.False(result[1].IsCurrent);
        Assert.False(result[1].IsRemote);
        Assert.Equal("origin/main", result[2].Name);
        Assert.True(result[2].IsRemote);
        Assert.Equal("origin/feature", result[3].Name);
        Assert.True(result[3].IsRemote);
    }

    [Fact]
    public void Parse_InvalidLine_IsSkipped()
    {
        var output = "invalid-line-without-pipe\nmain|*|refs/heads\n";

        var result = GitBranchParser.Parse(output);

        Assert.Single(result);
        Assert.Equal("main", result[0].Name);
    }
}
