using Branchy.Domain.Models;
using Branchy.Infrastructure.GitCli;
using Xunit;

namespace Branchy.Test;

public sealed class GitStatusParserTest
{
    [Fact]
    public void Parse_EmptyOutput_ReturnsDefaultBranch()
    {
        var result = GitStatusParser.Parse("/repo", "");

        Assert.Equal("HEAD", result.Branch.Name);
        Assert.Equal(0, result.Branch.AheadBy);
        Assert.Equal(0, result.Branch.BehindBy);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public void Parse_BranchHeader_ExtractsBranchName()
    {
        var output = "# branch.head main\n";

        var result = GitStatusParser.Parse("/repo", output);

        Assert.Equal("main", result.Branch.Name);
    }

    [Fact]
    public void Parse_BranchAheadBehind_ExtractsValues()
    {
        var output = "# branch.ab +3 -2\n";

        var result = GitStatusParser.Parse("/repo", output);

        Assert.Equal(3, result.Branch.AheadBy);
        Assert.Equal(2, result.Branch.BehindBy);
    }

    [Fact]
    public void Parse_UntrackedFile_ReturnsUntrackedChange()
    {
        var output = "? untracked.txt\n";

        var result = GitStatusParser.Parse("/repo", output);

        Assert.Single(result.Changes);
        Assert.Equal("untracked.txt", result.Changes[0].Path);
        Assert.Equal(FileChangeKind.Untracked, result.Changes[0].Kind);
        Assert.False(result.Changes[0].IsStaged);
    }

    [Fact]
    public void Parse_ModifiedUnstaged_ReturnsModifiedNotStaged()
    {
        var output = "1 .M N... 100644 100644 100644 abc123 def456 modified.txt\n";

        var result = GitStatusParser.Parse("/repo", output);

        Assert.Single(result.Changes);
        Assert.Equal("modified.txt", result.Changes[0].Path);
        Assert.Equal(FileChangeKind.Modified, result.Changes[0].Kind);
        Assert.False(result.Changes[0].IsStaged);
    }

    [Fact]
    public void Parse_ModifiedStaged_ReturnsModifiedStaged()
    {
        var output = "1 M. N... 100644 100644 100644 abc123 def456 staged.txt\n";

        var result = GitStatusParser.Parse("/repo", output);

        Assert.Single(result.Changes);
        Assert.Equal("staged.txt", result.Changes[0].Path);
        Assert.Equal(FileChangeKind.Modified, result.Changes[0].Kind);
        Assert.True(result.Changes[0].IsStaged);
    }

    [Fact]
    public void Parse_AddedFile_ReturnsAddedChange()
    {
        var output = "1 A. N... 000000 100644 100644 000000 abc123 newfile.txt\n";

        var result = GitStatusParser.Parse("/repo", output);

        Assert.Single(result.Changes);
        Assert.Equal(FileChangeKind.Added, result.Changes[0].Kind);
        Assert.True(result.Changes[0].IsStaged);
    }

    [Fact]
    public void Parse_DeletedFile_ReturnsDeletedChange()
    {
        var output = "1 D. N... 100644 000000 000000 abc123 000000 deleted.txt\n";

        var result = GitStatusParser.Parse("/repo", output);

        Assert.Single(result.Changes);
        Assert.Equal(FileChangeKind.Deleted, result.Changes[0].Kind);
    }

    [Fact]
    public void Parse_RenamedFile_ReturnsRenamedChange()
    {
        var output = "2 R. N... 100644 100644 100644 abc123 def456 R100 newname.txt\toldname.txt\n";

        var result = GitStatusParser.Parse("/repo", output);

        Assert.Single(result.Changes);
        Assert.Equal(FileChangeKind.Renamed, result.Changes[0].Kind);
    }

    [Fact]
    public void Parse_MultipleChanges_ReturnsAllChanges()
    {
        var output = """
            # branch.head feature
            # branch.ab +1 -0
            1 M. N... 100644 100644 100644 abc123 def456 staged.txt
            1 .M N... 100644 100644 100644 abc123 def456 unstaged.txt
            ? newfile.txt
            """;

        var result = GitStatusParser.Parse("/repo", output);

        Assert.Equal("feature", result.Branch.Name);
        Assert.Equal(1, result.Branch.AheadBy);
        Assert.Equal(0, result.Branch.BehindBy);
        Assert.Equal(3, result.Changes.Count);
    }

    [Fact]
    public void Parse_SetsRepositoryPath()
    {
        var result = GitStatusParser.Parse("/path/to/repo", "");

        Assert.Equal("/path/to/repo", result.RepositoryPath);
    }
}
