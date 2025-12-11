namespace Branchy.Domain.Models;

public enum FileChangeKind
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked
}

public sealed record FileChange(
    string Path,
    FileChangeKind Kind,
    bool IsStaged
);

public sealed record BranchStatus(
    string Name,
    int AheadBy,
    int BehindBy
);

public sealed record RepositoryStatus(
    string RepositoryPath,
    BranchStatus Branch,
    IReadOnlyList<FileChange> Changes
);
