namespace Branchy.UI.Models;

public sealed record Branch(
    string Name,
    bool IsCurrent,
    bool IsRemote
);
