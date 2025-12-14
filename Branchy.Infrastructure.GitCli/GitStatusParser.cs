using Branchy.Domain.Models;

namespace Branchy.Infrastructure.GitCli;

public static class GitStatusParser
{
    public static RepositoryStatus Parse(string repositoryPath, string statusOutput)
    {
        var lines = statusOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        string? branchName = null;
        int aheadBy = 0;
        int behindBy = 0;
        var changes = new List<FileChange>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                branchName = line.Substring("# branch.head ".Length).Trim();
                continue;
            }

            if (line.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                ParseAheadBehind(line, out aheadBy, out behindBy);
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("1 ", StringComparison.Ordinal) ||
                line.StartsWith("2 ", StringComparison.Ordinal) ||
                line.StartsWith("? ", StringComparison.Ordinal))
            {
                var change = ParseChangeLine(line);
                changes.Add(change);
            }
        }

        var branch = new BranchStatus(branchName ?? "HEAD", aheadBy, behindBy);

        return new RepositoryStatus(
            repositoryPath,
            branch,
            changes
        );
    }

    private static void ParseAheadBehind(string line, out int ahead, out int behind)
    {
        ahead = 0;
        behind = 0;

        var content = line.Substring("# branch.ab ".Length).Trim();
        var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (part.StartsWith("+", StringComparison.Ordinal))
            {
                int.TryParse(part.AsSpan(1), out ahead);
            }
            else if (part.StartsWith("-", StringComparison.Ordinal))
            {
                int.TryParse(part.AsSpan(1), out behind);
            }
        }
    }

    private static FileChange ParseChangeLine(string line)
    {
        if (line.StartsWith("? ", StringComparison.Ordinal))
        {
            var untrackedPath = line.Substring(2).Trim();
            return new FileChange(untrackedPath, FileChangeKind.Untracked, false);
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 8)
        {
            return new FileChange(line, FileChangeKind.Modified, false);
        }

        var xy = parts[1];
        var tabIndex = line.IndexOf('\t');
        var filePath = tabIndex >= 0 ? line[(tabIndex + 1)..].Trim() : parts[^1];

        var isStaged = xy.Length > 0 && xy[0] != '.';

        var kind = DetermineKind(xy);

        return new FileChange(filePath, kind, isStaged);
    }

    private static FileChangeKind DetermineKind(string xy)
    {
        var indexStatus = xy.Length > 0 ? xy[0] : '.';
        var workTreeStatus = xy.Length > 1 ? xy[1] : '.';

        if (indexStatus == 'A' || workTreeStatus == 'A')
        {
            return FileChangeKind.Added;
        }

        if (indexStatus == 'D' || workTreeStatus == 'D')
        {
            return FileChangeKind.Deleted;
        }

        if (indexStatus == 'R' || workTreeStatus == 'R')
        {
            return FileChangeKind.Renamed;
        }

        return FileChangeKind.Modified;
    }
}
