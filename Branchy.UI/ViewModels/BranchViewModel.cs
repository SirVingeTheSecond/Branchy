using Branchy.UI.Models;
using ReactiveUI;

namespace Branchy.UI.ViewModels;

public sealed class BranchViewModel : ReactiveObject
{
    public BranchViewModel(Branch branch)
    {
        Name = branch.Name;
        IsCurrent = branch.IsCurrent;
        IsRemote = branch.IsRemote;
        DisplayName = IsRemote ? Name.Replace("origin/", "") : Name;
    }

    public string Name { get; }

    public string DisplayName { get; }

    public bool IsCurrent { get; }

    public bool IsRemote { get; }
}
