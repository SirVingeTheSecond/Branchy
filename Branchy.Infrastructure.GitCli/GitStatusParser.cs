using Branchy.Domain.Models;

namespace Branchy.Infrastructure.GitCli;

public static class GitStatusParser
{
    public static RepositoryStatus Parse(string repositoryPath, string statusOutput)
    {
        var lines = statusOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        BranchStatus? branch = null;
        var changes = new List<FileChange>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                branch ??= ParseBranchLine(line);
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

        branch ??= new BranchStatus("HEAD", 0, 0);

        return new RepositoryStatus(
            repositoryPath,
            branch,
            changes
        );
    }

    private static BranchStatus ParseBranchLine(string line)
    {
        const string headPrefix = "# branch.head ";
        const string abPrefix = "# branch.ab ";

        if (line.StartsWith(headPrefix, StringComparison.Ordinal))
        {
            var name = line.Substring(headPrefix.Length).Trim();
            return new BranchStatus(name, 0, 0);
        }

        if (line.StartsWith(abPrefix, StringComparison.Ordinal))
        {
            var content = line.Substring(abPrefix.Length).Trim();
            var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var ahead = 0;
            var behind = 0;

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

            return new BranchStatus("HEAD", ahead, behind);
        }

        return new BranchStatus("HEAD", 0, 0);
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
